# Journal: Client Hosting (S3 + CloudFront)

This journal entry tells the story of how we designed the minimum deployment of the HexFall WebGL client: a private S3 bucket, a CloudFront distribution in front of it, and a public HTTPS URL. It is a design, not a record of a deployment. Nothing described here has been provisioned or tested yet.

The picture lives in [client-hosting.drawio](../diagrams/client-hosting.drawio). Open it in [diagrams.net](https://app.diagrams.net) or with the VS Code Draw.io extension.

## What we are actually shipping

We started by asking what a Unity WebGL build is, because the answer decides everything else. It is a folder of static files: an `index.html`, a JavaScript loader, a WebAssembly binary, and a data file with the game assets. Unity emits them pre-compressed with Brotli (`.br`), and the whole build is about 18 MB.

Static files need no compute. Nothing runs on the server to deliver them. That settled the goal: a public HTTPS URL with the fewest moving parts. DD-004 chose WebGL so that players can try the game from a link, and DD-007 already chose S3 + CloudFront to host it. This document works out the details those two decisions left open.

## Why not a VPC

The first instinct was to use the account's default VPC. It is a reasonable instinct, since most AWS tutorials start there. But it does not apply to this part of the system.

S3 and CloudFront are managed services. They are not deployed into a VPC at all. S3 is a regional service reached through AWS public endpoints, and CloudFront is a global edge network. There are no subnets to choose, no security groups to write, and no NAT gateway to pay for.

The default VPC will matter later, for the game server. The ALB and the ECS Fargate tasks (DD-005, DD-006) do live inside a VPC. Until then, the client hosting design simply has no network layer to design.

## Why S3

The alternative that comes to mind first is a small web server, on EC2 or in a container on ECS, serving the files. We ruled it out because it buys nothing. A server has to be patched, sized and paid for around the clock, in order to hand out files that never change between deploys.

S3 has none of that. There are no servers to patch, and cost is per use (storage plus requests). It is available in `eu-west-1`, which satisfies the region lock described in [AWS_ENVIRONMENT.md](../AWS_ENVIRONMENT.md).

We also looked at AWS Amplify Hosting. It would work, but it adds another service with its own build pipeline and its own concepts, and we already have a build process (`tools/build-webgl.ps1`) and a plan for CI. Amplify would solve a problem we do not have.

## Why the bucket stays private

The obvious way to serve files from S3 is to make the bucket public. We chose the opposite: the bucket is never public, and all access goes through CloudFront. That keeps one front door, with one set of rules, and it means a misconfigured policy cannot accidentally expose the bucket to the internet.

Four settings make that concrete.

- **Block Public Access, all four settings on.** This is a safety net on top of the bucket policy. Even if someone later writes a policy that grants public access, S3 refuses to honor it.
- **Object ownership set to `BucketOwnerEnforced`.** This disables ACLs, so the only access mechanism left is policy. Fewer mechanisms means fewer places for a mistake.
- **Default encryption with SSE-S3.** Objects are encrypted at rest with keys S3 manages. We considered SSE-KMS and decided against it. With KMS, CloudFront would need explicit permission in the key policy, which means an extra resource, extra cost and extra moving parts, all to protect game files that are public by design.
- **Versioning is a parameter.** Off in dev, where it only accumulates old builds. In production, versioning gives a way to roll back a bad upload. It is a variable, so each environment decides.

One more parameter deserves a note: `force_destroy`. With it set to `true`, `terraform destroy` can delete a bucket that still holds files. We default it to `true` for dev, where tearing everything down cleanly matters more than protecting the contents, and to `false` everywhere else.

## Why CloudFront in front

With a private bucket, something has to sit in front of it. CloudFront is that something, and it brings three benefits we would want anyway.

It gives us HTTPS for free on the default `*.cloudfront.net` domain, with no certificate to request. It caches files at the edge, so repeat visitors and visitors far from `eu-west-1` get the 18 MB build faster. And HTTPS is the right default for us anyway. Browsers increasingly restrict what a plain-HTTP page may do, and under mixed-content rules a page served over HTTPS may only open secure connections: when the game later talks to the ALB (DD-005), it will use `wss://`, never plain `ws://`. Serving the page over HTTPS from day one commits the whole path to encryption instead of discovering the constraint later.

## How CloudFront reads a private bucket

The private bucket raises an obvious question: if nobody can read it, how does CloudFront? There were three candidate answers.

The S3 website endpoint is the oldest. We ruled it out quickly. It only works when the bucket is public, and CloudFront can only talk to it over HTTP, which defeats the purpose.

Origin Access Identity (OAI) is the older private-bucket mechanism. It works, but AWS now treats it as legacy and recommends its replacement.

Origin Access Control (OAC) is that replacement, and it is what we chose. CloudFront signs each request to S3 with SigV4, and the bucket policy trusts CloudFront's service principal rather than a special identity.

The bucket policy is where the security lives, so it is worth reading closely. It allows `s3:GetObject` to `cloudfront.amazonaws.com`, but only when `AWS:SourceArn` equals the ARN of this specific distribution. Without that condition, any CloudFront distribution in any account could be pointed at our bucket. The condition closes that gap; it is the standard guard against the confused-deputy problem.

Two consequences follow, and we accept both.

- The policy grants only `GetObject`, not `s3:ListBucket`. When S3 cannot tell whether a key exists, a request for a missing file returns `403` instead of `404`. That is slightly less friendly, but the alternative is letting CloudFront list the bucket, which we do not want.
- The bucket policy and the distribution reference each other (the policy needs the distribution ARN, and the distribution needs the bucket). Terraform handles this ordering for us, which is one more argument for provisioning it with Terraform.

## Distribution settings, and why each

| Setting | Value | Why |
|---|---|---|
| Default root object | `index.html` | So the bare URL serves the game. |
| Viewer protocol policy | `redirect-to-https` | Anyone who types or follows an `http://` link is sent to HTTPS. |
| Certificate | CloudFront default | A custom domain needs an ACM certificate in `us-east-1`, which the region-lock SCP likely blocks (see the known risks in AWS_ENVIRONMENT.md). The default certificate covers `*.cloudfront.net`. |
| Price class | `PriceClass_100` | The cheapest class, covering North America and Europe, where players and the lab are. Exposed as a parameter. |
| HTTP versions | HTTP/2 and HTTP/3 | Better multiplexing for a handful of large files, at no cost. |
| IPv6 | On | No reason to leave players on IPv6 networks behind. |
| WAF, access logs | Not in the minimum | Cost and scope. Listed under future work. |

A custom domain is deliberately not part of this design. If we find a way to get a certificate, it becomes an additive change to the module, not a rewrite.

## Brotli and metadata

This is the part that is easy to get wrong, and the part that breaks the game if it is wrong.

Unity writes files like `Build/WebGL.wasm.br`. The bytes are already Brotli-compressed. For the browser to use them, the response must say so with `Content-Encoding: br`, and it must carry the real type of the underlying file in `Content-Type`. CloudFront does not compress responses that already have a `Content-Encoding`, so it will not fix this for us. If the metadata is missing, the browser receives compressed bytes it does not know how to decode, and loading fails.

So the metadata has to be set on each object when it is uploaded:

| File | Content-Type | Content-Encoding |
|---|---|---|
| `*.wasm.br` | `application/wasm` | `br` |
| `*.js.br` (framework) | `application/javascript` | `br` |
| `*.data.br` | `application/octet-stream` | `br` |
| `index.html` | `text/html` | none |
| `*.loader.js` | `application/javascript` | none |

This is exactly the rule that `tools/serve_webgl.py` already applies for local testing, documented in [BUILD.md](../BUILD.md). Production simply has to match what we already verified locally.

The trade-off is that clients without Brotli support cannot load the game. Every modern browser supports Brotli over HTTPS, and we are already HTTPS-only, so we accept it.

## Caching strategy

We use CloudFront's managed `CachingOptimized` policy. It is sensible defaults we do not have to maintain.

The catch is that Unity does not put content hashes in file names by default. A new build produces files with the same names as the old one, so cached copies at the edge would keep serving stale game files after a deploy. The simple answer is to invalidate the distribution (`/*`) at the end of every deploy. The first 1,000 invalidation paths per month are free, and one wildcard path per deploy is a single path, so this costs nothing for our usage.

There is a better answer for later. Unity has a Player Settings option to name build files after their hashes. With hashed names, the large files can be cached for a long time and only `index.html` needs invalidating. That is a worthwhile optimization once deploys become frequent, and we note it as future work rather than adding the complexity now.

## Who does what: Terraform and the deploy script

We were careful to separate two jobs that are easy to blur.

**Terraform creates and maintains the infrastructure:** the bucket, its settings, the distribution, the bucket policy. **A script deploys builds:** it uploads files with the right metadata and invalidates the cache. We confirmed this split with the project owner.

The reasoning is that these have different lifecycles. Infrastructure changes rarely, and each change deserves a reviewed plan. A new build happens every time we want to publish, possibly many times a day, and should be a quick command with no plan to read.

The alternative we rejected was managing every file with `aws_s3_object`. It would put each build artifact into Terraform state and into every plan. An 18 MB build with changing hashes would make plans noisy and slow, and per-file `Content-Encoding` rules are simpler to express in a short script than in a resource per object.

The split also helps CI. The same script a developer runs locally is what the pipeline will call. And the two sides talk through a narrow contract: Terraform outputs the bucket name, the distribution id and the URL, and the deploy script reads them. Nothing else crosses the boundary. In the diagram, this is the dashed arrow from the Terraform CLI to the deploy script.

## Making it agnostic and parameterized

The design should not be welded to one account or one environment. Two layers give us that.

- `infra/modules/static-site` is a reusable module that holds the bucket, the distribution, the OAC and the policy. It knows nothing about a particular account.
- `infra/envs/dev` is a thin composition that calls the module with dev-sized values. A future `prod` is another thin composition.

Nothing is hard-coded to an account. S3 bucket names are global across all AWS accounts, so a fixed name would collide. We build it as `hexfall-<environment>-client-<account-id>`, taking the account id from `data.aws_caller_identity` at plan time. The region is a variable that defaults to `eu-west-1` because of the SCP.

The state backend is configured partially: the Terraform block names the S3 backend, and each environment supplies a `backend.hcl` with the bucket and key. State uses native locking with `use_lockfile`, as decided in DD-009. The provider sets `default_tags` (`Project`, `Environment`, `ManagedBy = terraform`) so every resource is tagged without repeating it.

These are the planned inputs:

| Variable | Type | Default | Why |
|---|---|---|---|
| `project` | string | `hexfall` | Feeds the bucket name and tags. |
| `environment` | string | none (required) | Distinguishes dev from prod and feeds the bucket name. |
| `aws_region` | string | `eu-west-1` | The region lock allows nothing else. |
| `price_class` | string | `PriceClass_100` | Cheapest class covering players and lab. |
| `enable_versioning` | bool | `false` | Off in dev, on where rollback matters. |
| `force_destroy` | bool | `false` | Dev turns it on for easy teardown; everything else keeps it safe. |
| `default_root_object` | string | `index.html` | What the bare URL serves. |
| `tags` | map(string) | `{}` | Extra tags merged with the defaults. |

And the planned outputs, which are the contract with the deploy script:

| Output | Used for |
|---|---|
| `bucket_name` | Target of `aws s3 sync`. |
| `bucket_arn` | Policies or tooling that need the ARN. |
| `distribution_id` | Target of the invalidation. |
| `distribution_domain_name` | The `dxxxx.cloudfront.net` host. |
| `url` | The full `https://` address to share and smoke-test. |

A word of honesty about the word "agnostic". Here it means agnostic to account, environment and region. It does not mean agnostic to cloud provider. S3 and CloudFront are AWS-specific, and moving to another provider would mean rewriting the module.

## Checking the account constraints

[AWS_ENVIRONMENT.md](../AWS_ENVIRONMENT.md) lists the rules this lab account enforces. We checked the design against each.

- **IAM naming and boundary:** this design creates no IAM roles. OAC uses a service principal in a bucket policy, not a role, so the `student-*` and `StudentBoundary` rule is not triggered.
- **Region lock:** S3 is regional and lives in `eu-west-1`, which is allowed. CloudFront is global and works under the lock.
- **Permissions to create:** that document records that read access to S3 and CloudFront was verified, and that create access must be confirmed. We still need to confirm it on the first `terraform apply`. If creating a distribution is denied, we find out immediately and early.

## Cost at a glance

We will not quote prices here. For a course project with a small audience the bill should be negligible: an 18 MB build is a tiny amount of S3 storage, a handful of players generate little CloudFront transfer, one wildcard invalidation per deploy stays within the free invalidation paths, and `PriceClass_100` keeps edge costs at the low end. Whether the lab account qualifies for AWS free-tier offers is unknown (some of them apply only to new accounts), so we will check the account's billing once the stack exists rather than assume it is free.

## What comes next

1. **Terraform implementation:** bootstrap the state bucket in `infra/bootstrap/`, write the `static-site` module, and compose `infra/envs/dev`.
2. **Deploy script:** `tools/deploy-webgl`, which reads the Terraform outputs, syncs with the metadata above and invalidates the cache.
3. **CI:** a pipeline that builds, deploys and smoke-tests the URL.
4. **The game server side:** the ALB and Fargate service (DD-005, DD-006), which is where the default VPC finally enters the picture.

Possible later improvements noted above: a custom domain if a certificate becomes possible, hashed file names with long TTLs, WAF and access logs, and versioning in production.

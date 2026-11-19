# Decision Journal

These entries narrate **how** we reached a design: the questions we asked, the options we weighed, and why some were ruled out. They are written while the work is being designed, so they can describe things that are not built yet.

The final, implemented result of each decision is recorded separately in [DESIGN_DECISIONS.md](../DESIGN_DECISIONS.md), without the path that led to it.

| Entry | Topic |
|---|---|
| [client-hosting.md](client-hosting.md) | Minimum deployment of the WebGL client on S3 + CloudFront, provisioned with Terraform |
| [ci-cd.md](ci-cd.md) | Pipeline that builds, deploys and smoke-tests the client: OIDC, runtime config, upload order |

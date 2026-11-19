namespace HexFall.UI
{
    public readonly struct PsxUiPresentation
    {
        public const string RootClass = "hf-psx-ui";

        public PsxUiPresentation(bool renderToTexture, string rootClass, float compositeFlag)
        {
            RenderToTexture = renderToTexture;
            Class = rootClass;
            CompositeFlag = compositeFlag;
        }

        public bool RenderToTexture { get; }
        public string Class { get; }
        public float CompositeFlag { get; }

        public static PsxUiPresentation Resolve(bool psxUiEnabled) =>
            psxUiEnabled
                ? new PsxUiPresentation(true, RootClass, 1f)
                : new PsxUiPresentation(false, null, 0f);
    }
}

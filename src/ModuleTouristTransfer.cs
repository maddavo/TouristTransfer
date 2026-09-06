namespace TouristTransfer
{
    public sealed class ModuleTouristTransfer : PartModule
    {
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Tourist Transfer...")]
        public void OpenTouristTransfer()
        {
            if (TouristTransferWindow.Instance != null)
                TouristTransferWindow.Instance.Open(part);
        }
    }
}

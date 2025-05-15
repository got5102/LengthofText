using TextLength.Models;

namespace TextLength.Services
{
    public interface IOverlayService
    {
        void ShowOverlay(TextSelectionInfo selectionInfo);
        void HideOverlay();
        void UpdateSettings();
    }
}
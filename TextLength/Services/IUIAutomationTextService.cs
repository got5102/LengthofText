using System.Threading.Tasks;
using TextLength.Models;

namespace TextLength.Services
{
    public interface IUIAutomationTextService
    {
        Task<TextSelectionInfo?> GetSelectedTextInfoAsync();
    }
} 
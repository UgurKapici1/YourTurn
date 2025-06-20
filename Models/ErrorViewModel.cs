namespace YourTurn.Web.Models;

// Hata sayfasında gösterilecek verileri temsil eden model
public class ErrorViewModel
{
    // İstek kimliği
    public string? RequestId { get; set; }

    // İstek kimliğinin gösterilip gösterilmeyeceğini belirler
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

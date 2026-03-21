using System;

namespace Zabrownie.Models
{
    public class HistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime VisitDate { get; set; } = DateTime.Now;
    }
}

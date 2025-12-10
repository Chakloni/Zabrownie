using System;

namespace Zabrownie.Models
{
    public class Bookmark
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Folder { get; set; } = "Unsorted";
        public DateTime DateAdded { get; set; }
    }
}
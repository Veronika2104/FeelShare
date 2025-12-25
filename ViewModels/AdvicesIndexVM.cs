using System.Collections.Generic;
using FeelShare.Web.Models;

namespace FeelShare.Web.Areas.Admin.ViewModels
{
    public class AdvicesIndexVM
    {
        public List<MoodAdvice> List { get; set; } = new();
        public MoodAdvice Form { get; set; } = new();
        public string? Query { get; set; }
    }
}
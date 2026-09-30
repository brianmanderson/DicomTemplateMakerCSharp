using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DicomTemplateMakerGUI.Shell
{
    /// <summary>Small helpers for readable messages.</summary>
    internal static class Text
    {
        public const string Bullet = "  • ";

        /// <summary>"1 template", "3 templates".</summary>
        public static string Count(int count, string singular, string? plural = null)
        {
            return count == 1 ? "1 " + singular : count + " " + (plural ?? singular + "s");
        }

        /// <summary>"is" or "are".</summary>
        public static string IsAre(int count)
        {
            return count == 1 ? "is" : "are";
        }

        /// <summary>One bulleted line per item, at most <paramref name="max"/>, then "… and N more.".</summary>
        public static string List(IEnumerable<string> items, int max = 15, string prefix = Bullet)
        {
            List<string> all = items.ToList();
            var text = new StringBuilder();
            foreach (string item in all.Take(max))
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(prefix).Append(item);
            }

            if (all.Count > max)
            {
                text.AppendLine().Append(prefix).Append("… and ").Append(all.Count - max).Append(" more.");
            }

            return text.ToString();
        }
    }
}

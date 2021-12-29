using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.DiscountRules.HasCategory.Models
{
    public record RequirementModel
    {
        public int DiscountId { get; set; }

        public int RequirementId { get; set; }

        [NopResourceDisplayName("Plugins.DiscountRules.HasCategory.Fields.Categories")]
        public string CategoryIds { get; set; }
    }
}
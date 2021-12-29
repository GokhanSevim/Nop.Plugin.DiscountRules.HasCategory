
namespace Nop.Plugin.DiscountRules.HasCategory
{
    /// <summary>
    /// Represents constants for the discount requirement rule
    /// </summary>
    public static class DiscountRequirementDefaults
    {
        /// <summary>
        /// The HTML field prefix for discount requirements
        /// </summary>
        public const string HTML_FIELD_PREFIX = "DiscountRulesHasCategory{0}";

        /// <summary>
        /// The key of the settings to save restricted category identifiers
        /// </summary>
        public const string SETTINGS_KEY = "DiscountRequirement.RestrictedCategoryIds-{0}";

        /// <summary>
        /// The system name of the discount requirement rule
        /// </summary>
        public const string SYSTEM_NAME = "DiscountRequirement.HasCategory";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Plugin.DiscountRules.HasCategory.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;

namespace Nop.Plugin.DiscountRules.HasCategory
{
    public partial class HasCategoryDiscountRequirementRule : BasePlugin, IDiscountRequirementRule
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IDiscountService _discountService;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly ICategoryService _categoryService;

        #endregion

        #region Ctor

        public HasCategoryDiscountRequirementRule(IActionContextAccessor actionContextAccessor,
            IDiscountService discountService,
            ILocalizationService localizationService,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            ICategoryService categoryService)
        {
            _actionContextAccessor = actionContextAccessor;
            _discountService = discountService;
            _localizationService = localizationService;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _categoryService = categoryService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check discount requirement
        /// </summary>
        /// <param name="request">Object that contains all information required to check the requirement (Current customer, discount, etc)</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            //invalid by default
            var result = new DiscountRequirementValidationResult();

            //try to get saved restricted category identifiers
            var restrictedCategoryIds = await _settingService.GetSettingByKeyAsync<string>(string.Format(DiscountRequirementDefaults.SETTINGS_KEY, request.DiscountRequirementId));
            if (string.IsNullOrWhiteSpace(restrictedCategoryIds))
            {
                //valid
                result.IsValid = true;
                return result;
            }

            if (request.Customer == null)
                return result;

            //we support three ways of specifying categories:
            //1. The comma-separated list of category identifiers (e.g. 77, 123, 156).
            //2. The comma-separated list of category identifiers with quantities.
            //      {Category ID}:{Quantity}. For example, 77:1, 123:2, 156:3
            //3. The comma-separated list of category identifiers with quantity range.
            //      {Category ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
            var restrictedCategories = restrictedCategoryIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            if (!restrictedCategories.Any())
                return result;

            //group categories in the cart by product ID
            //it could be the same category with distinct category attributes
            //that's why we get the total quantity of this category            
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer: request.Customer, shoppingCartType: ShoppingCartType.ShoppingCart, storeId: request.Store.Id);

            if (cart != null && cart.Count > 0)
            {
                List<CartModel> cartList = new();

                cart.ToList().ForEach((item) =>
                {
                    var category = _categoryService.GetProductCategoriesByProductIdAsync(item.ProductId, false).Result;

                    cartList.Add(new CartModel()
                    {
                        CartItem = item,
                        Category = category
                    });

                });

                foreach (var restrictedCategory in restrictedCategories)
                {
                    if (string.IsNullOrWhiteSpace(restrictedCategory))
                        continue;

                    ushort CaseCode = 0;
                    int restrictedCategoryId = 0;
                    int quantityMin = 0;
                    int quantityMax = 0;
                    int quantity = 0;
                    int TotalQuantity = 0;

                    if (restrictedCategory.Contains(":"))
                    {
                        if (restrictedCategory.Contains("-"))
                        {
                            CaseCode = 1;

                            //the third way (the quantity rage specified)
                            //{Category ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
                            if (!int.TryParse(restrictedCategory.Split(new[] { ':' })[0], out restrictedCategoryId))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedCategory.Split(new[] { ':' })[1].Split(new[] { '-' })[0], out quantityMin))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedCategory.Split(new[] { ':' })[1].Split(new[] { '-' })[1], out quantityMax))
                                //parsing error; exit;
                                return result;
                        }
                        else
                        {
                            CaseCode = 2;

                            //the second way (the quantity specified)
                            //{Category ID}:{Quantity}. For example, 77:1, 123:2, 156:3
                            if (!int.TryParse(restrictedCategory.Split(new[] { ':' })[0], out restrictedCategoryId))
                                //parsing error; exit;
                                return result;
                            if (!int.TryParse(restrictedCategory.Split(new[] { ':' })[1], out quantity))
                                //parsing error; exit;
                                return result;
                        }
                    }
                    else
                    {   
                        if (int.TryParse(restrictedCategory, out restrictedCategoryId))
                        {
                            CaseCode = 3;
                        }
                    }

                    var Find = cartList.Where(x => x.Category.Select(p => p.CategoryId).Contains(restrictedCategoryId)).ToList();

                    if (Find != null && Find.Count > 0)
                    {
                        TotalQuantity = Find.Sum(x => x.CartItem.Quantity);

                        switch (CaseCode)
                        {
                            case 1:

                                if (quantityMin > 0 && quantityMin <= TotalQuantity && TotalQuantity <= quantityMax)
                                {
                                    result.IsValid = true;
                                    return result;
                                }

                                break;
                            case 2:

                                if (quantity > 0 && TotalQuantity == quantity)
                                {
                                    result.IsValid = true;
                                    return result;
                                }

                                break;
                            case 3:                         

                                result.IsValid = true;
                                return result;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get URL for rule configuration
        /// </summary>
        /// <param name="discountId">Discount identifier</param>
        /// <param name="discountRequirementId">Discount requirement identifier (if editing)</param>
        /// <returns>URL</returns>
        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

            return urlHelper.Action("Configure", "DiscountRulesHasCategory",
                new { discountId = discountId, discountRequirementId = discountRequirementId }, _webHelper.GetCurrentRequestProtocol());
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //locales
            await _localizationService.AddLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.DiscountRules.HasCategory.Fields.Categories"] = "Restricted Categories [and quantity range]",
                ["Plugins.DiscountRules.HasCategory.Fields.Categories.Hint"] = "The comma-separated list of Category identifiers (e.g. 77, 123, 156). You can find a Category ID on its details page. You can also specify the comma-separated list of Category identifiers with quantities ({Category ID}:{Quantity}. for example, 77:1, 123:2, 156:3). And you can also specify the comma-separated list of Category identifiers with quantity range ({Category ID}:{Min quantity}-{Max quantity}. for example, 77:1-3, 123:2-5, 156:3-8).",
                ["Plugins.DiscountRules.HasCategory.Fields.Categories.AddNew"] = "Add Category",
                ["Plugins.DiscountRules.HasCategory.Fields.Categories.Choose"] = "Choose",
                ["Plugins.DiscountRules.HasCategory.Fields.CategoryIds.Required"] = "Categories are required",
                ["Plugins.DiscountRules.HasCategory.Fields.DiscountId.Required"] = "Discount is required",
                ["Plugins.DiscountRules.HasCategory.Fields.CategoryIds.InvalidFormat"] = "Invalid format for Categories selection. Format should be comma-separated list of Category identifiers (e.g. 77, 123, 156). You can find a Category ID on its details page. You can also specify the comma-separated list of Category identifiers with quantities ({Category ID}:{Quantity}. for example, 77:1, 123:2, 156:3). And you can also specify the comma-separated list of Category identifiers with quantity range ({Category ID}:{Min quantity}-{Max quantity}. for example, 77:1-3, 123:2-5, 156:3-8)."
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //discount requirements
            var discountRequirements = (await _discountService.GetAllDiscountRequirementsAsync())
                .Where(discountRequirement => discountRequirement.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SYSTEM_NAME);
            foreach (var discountRequirement in discountRequirements)
            {
                await _discountService.DeleteDiscountRequirementAsync(discountRequirement, false);
            }

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.DiscountRules.HasCategory");

            await base.UninstallAsync();
        }

        #endregion
    }
}
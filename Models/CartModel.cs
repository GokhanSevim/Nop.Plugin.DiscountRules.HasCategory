using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Catalog;

namespace Nop.Plugin.DiscountRules.HasCategory.Models
{
    public record CartModel
    {
        public ShoppingCartItem CartItem { get; set; }

        public IList<ProductCategory> Category { get; set; }
    }
}

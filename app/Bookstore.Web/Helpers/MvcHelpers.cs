using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
// TODO configuration-migration: Replace System.Web.SessionState with ASP.NET Core session

namespace Bookstore.Web.Helpers
{
    public static class MvcHelpers
    {
        public static IEnumerable<SelectListItem> GetSelectListForEnum<T>(this HtmlHelper html, string emptyItem = null)
            where T : Enum
        {
            if (!string.IsNullOrEmpty(emptyItem))
            {
                yield return new SelectListItem()
                {
                    Text = emptyItem
                };
            }
            foreach (var val in Enum.GetValues(typeof(T)))
            {
                yield return new SelectListItem()
                {
                    Text = Enum.GetName(typeof(T), val)
                };
            }

        }
    }
}
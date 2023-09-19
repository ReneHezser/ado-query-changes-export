using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.WebApi;

namespace PluginBase
{
   public static class Extensions
   {
      /// <summary>
      /// Get the field value as a string, or other type if specified.
      /// </summary>
      /// <param name="field"></param>
      /// <param name="type"></param>
      /// <returns></returns>
      public static object GetFieldValue(dynamic field, Type? type = null)
      {
         if (type == null) type = field.GetType();

         if (field == null)
         {
            return string.Empty;
         }

         if (type == typeof(string))
         {
            return field;
         }

         if (type == typeof(DateTime))
         {
            return (DateTime)field;
         }

         if (type == typeof(IEnumerable))
         {
            var values = new List<string>();
            foreach (var value in field)
            {
               values.Add(value.ToString());
            }
            return string.Join(", ", values);
         }

         if (type == typeof(IdentityRef))
         {
            return ((IdentityRef)field).DisplayName;
         }

         return field.ToString();
      }
   }
}
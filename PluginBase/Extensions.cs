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
      public static T GetFieldValue<T>(dynamic field)
      {
         var fieldType = field.GetType();

         if (fieldType == null)
         {
            return (T)(object)string.Empty;
         }

         if (fieldType == typeof(string))
         {
            return (T)field;
         }

         if (fieldType == typeof(DateTime))
         {
            if (typeof(T) == typeof(DateTime))
            {
               return (T)field;
            }
            if (typeof(T) == typeof(string))
            {
               return (T)(object)((DateTime)field).ToString();
            }
         }

         if (fieldType == typeof(IEnumerable))
         {
            var values = new List<string>();
            foreach (var value in field)
            {
               values.Add(value.ToString());
            }
            return (T)(object)string.Join(", ", values);
         }

         if (fieldType == typeof(IdentityRef))
         {
            return (T)(object)((IdentityRef)field).DisplayName;
         }

         return field.ToString();
      }
   }
}
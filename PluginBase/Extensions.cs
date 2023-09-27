using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

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

      /// <summary>
      /// get the description attribute of a property
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="fieldName"></param>
      /// <returns></returns>
      public static string GetDescription<T>(string fieldName)
      {
         string result = null;
         PropertyInfo pi = typeof(T).GetProperty(fieldName.ToString());
         if (pi != null)
         {
            try
            {
               object[] descriptionAttrs = pi.GetCustomAttributes(typeof(DescriptionAttribute), false);
               if (descriptionAttrs.Length != 0)
               {
                  DescriptionAttribute description = (DescriptionAttribute)descriptionAttrs[0];
                  result = (description.Description);
               }
            }
            catch
            {
               result = null;
            }
         }

         return result;
      }

      /// <summary>
      /// Update the ADO item with the changes from a class. Decorate the properties of the class with a DescriptionAttribute that matches the ADO field name.
      /// </summary>
      /// <param name="workItem"></param>
      public static JsonPatchDocument UpdateAdoItem<T>(this T pluginClass, WorkItem workItem, ILogger logger = null)
      {
         JsonPatchDocument patches = new JsonPatchDocument();

         var properties = typeof(T).GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
         foreach (var property in properties)
         {
            var propertyValue = property.GetValue(pluginClass);
            if (propertyValue != null)
            {
               var adoFieldName = GetDescription<T>(property.Name);
               // properties that do not have a description are ignored
               if (adoFieldName == null) continue;

               // do not update fields that did not change (have already been changed)
               if (string.Equals(GetFieldValue<string>(workItem.Fields[adoFieldName]), (propertyValue ?? string.Empty).ToString(), StringComparison.OrdinalIgnoreCase))
                  continue;

               logger?.LogDebug($"\tUpdating {adoFieldName} to {propertyValue}");
               patches.Add(new JsonPatchOperation
               {
                  Operation = Operation.Add,
                  Path = "/fields/" + adoFieldName,
                  Value = propertyValue
               });
               workItem.Fields[adoFieldName] = propertyValue;
            }
         }

         return patches;
      }
   }
}
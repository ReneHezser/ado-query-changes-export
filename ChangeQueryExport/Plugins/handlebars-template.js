<html>
   <head></head>
   <style>
      table {
         width: 100%;
      }

      table, th, td {
         border: 1px solid lightgrey;
         border-collapse: collapse;
      }

      th, td {
         padding: 2px 4px;
      }

      .system-rev {
         background-color: lightgrey;
      }
      .system-state {
         background-color: lightgreen;
      }

      img.arrow-up {
         width:16px;
         height:16px;
         background:url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAEDSURBVDhPY0AG7OzspszMzKeYmJiUoULEAy4uLglGRsbbQOZ/IH1SSEiIByJDBJCSkmID2rofyPwPw0D+SpAcUYCFhWU+kIJrhmGgd1qBNH7AxsbWAKQwNIMw0Cv/gYanAtm4AdCpz4DUVyD+CMTfgRik+RuU/xMof1xdXZ0JyMYOgDaIAyk5IBYB4gIgBhmQAsSiQCwHdCGIJhpEAzHIAF8wjwiA7jQYnxlKEwS4/UYkGGQGAPMCC5QJCkiiAIoB3NzcnMDEA4paVqgQQcAIpcEAmGh0/v//fwFoyGsODo57QDY+L7L8+/fvJooBIAC0PQJoQA8PD480yDX4wM+fP28BAF+SN5HMGXP5AAAAAElFTkSuQmCC')
      }
   </style>
   <body>
      <h1>{{ title }}</h1>
      <table>
         <thead>
            <th>ID - Title</th><th>Changes</th>
         </thead>
         <tbody>
            {{ #each reportItems }}
            <tr>
               <td>
                  <a href="{{ this.LinkToParent }}" target=_blank><img class="arrow-up"></a>
                  <a href="{{ this.LinkToItem }}" target=_blank>{{ this.ID }} - {{ this.Title }}</a>
               </td>
               <td>
                  <table>
                     <thead>
                        <tr>
                           <th>Field Name</th>
                           <th>Old Value</th>
                           <th>New Value</th>
                        </tr>
                     </thead>
                     <tbody>
                        {{ #each this.ChangedFields }}
                        <tr {{#StringEqualityBlockHelper this.Key 'System.Rev'}}class="system-rev"{{/StringEqualityBlockHelper}}
                        {{#StringEqualityBlockHelper this.Key 'System.State'}}class="system-state"{{/StringEqualityBlockHelper}}
                        >
                           <td>{{ this.Key }}</td>
                           <td>{{{this.previousValue}}}</td>
                           <td>{{{this.currentValue}}}</td>
                        </tr>
                        {{/ each}}
                     </tbody>
                  </table>
               </td>
            </tr>
            {{/ each}}
         </tbody>
      </table>
   </body>
</html >
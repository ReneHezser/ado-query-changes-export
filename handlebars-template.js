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
               <td>{{ this.ID }} - {{ this.Title }}</td>
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
                        <tr>
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
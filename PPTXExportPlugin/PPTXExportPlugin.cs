using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Drawing = DocumentFormat.OpenXml.Drawing;
using System.Text.RegularExpressions;

namespace PPTXExportPlugin
{
    public class PPTXExportPlugin : IPlugin
    {
        public string Name { get => "PPTX Export Plugin"; }
        public string Description { get => "Exports changes to an PPTX file."; }
        public ILogger Logger { get; set; }
        public IDictionary<string, string> Errors { get; private set; } = new Dictionary<string, string>();

        public static string[] IgnoreFieldsStartingWith { get; set; } = new[] {
            "System.BoardColumn", "Microsoft.VSTS.", "WEF_", "Custom."
        };

        public int Execute(List<IReportItem> items)
        {
            if (Logger is null) throw new ArgumentNullException(nameof(Logger));

            Logger.LogInformation("PPTX export plugin started.");
            CreatePPTX(items);
            Logger.LogInformation("PPTX export plugin finished.");
            return 0;
        }

        /// Remove all fields that start with the given strings
        private IReportItem[] FilterItems(List<IReportItem> workItems)
        {
            var result = new List<IReportItem>();
            foreach (var item in workItems.ToArray())
            {
                result.Add(item);
                var remainingFields = new List<IChangedField>();

                foreach (var field in item.ChangedFields)
                {
                    if (IgnoreFieldsStartingWith.Any(field.Key.StartsWith))
                        continue;

                    remainingFields.Add(field);
                }
                item.ChangedFields = remainingFields;
            }
            return workItems.ToArray();
        }

        private static string ReformatHTML(string stringHTML)
        {
            // Look for HTML content which can be removed or converted into text.
            stringHTML = stringHTML.Replace("&nbsp;", Environment.NewLine, StringComparison.InvariantCultureIgnoreCase);
            stringHTML = stringHTML.Replace("&lt;", ">", StringComparison.InvariantCultureIgnoreCase);
            stringHTML = stringHTML.Replace("&gt;", "<", StringComparison.InvariantCultureIgnoreCase);
            stringHTML = stringHTML.Replace("&nbsp;", " ", StringComparison.InvariantCultureIgnoreCase);
            stringHTML = Regex.Replace(stringHTML, "<.*?>", string.Empty, RegexOptions.Multiline);
            stringHTML = Regex.Replace(stringHTML, @"^\s+$", string.Empty, RegexOptions.Multiline);
            stringHTML = Regex.Replace(stringHTML, @"^\n|\r$", string.Empty, RegexOptions.Multiline);
            // stringHTML = Regex.Replace(stringHTML, @"^\s*", string.Empty, RegexOptions.Multiline);
            return stringHTML;
        }

        internal void CreatePPTX(List<IReportItem> workItems)
        {
            // Filter unneeded ReportItem.ChangedFields.
            var ReportItems = FilterItems(workItems);

            // Check if PowerPoint template exists and can be accessed.
            string source;
            var relativeTemplateFile = Path.Combine(new[] { "Plugins", "PPTXExportPlugin_Template.pptx" });
            try
            {
                source = File.ReadAllText(relativeTemplateFile);
            }
            catch (FileNotFoundException)
            {
                Logger.LogCritical("Cannot find the PPTX template 'PPTXExportPlugin_Template.pptx' in the Plugins folder. Please make sure it is there and try again.");
                return;
            }

            // Use the target file from the environment variable if it exists, otherwise use the default which will put the output file into the application folder
            string targetFile = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PPTX_TARGET_PATH")) ?
                $"{Environment.GetEnvironmentVariable("ORGANIZATION")}-{Environment.GetEnvironmentVariable("PROJECT")}-{DateTime.Now.ToShortDateString().Replace("/", "-")}-Export.pptx" :
                Environment.GetEnvironmentVariable("PPTX_TARGET_PATH");

            try
            {
                string templateFilePath = Path.GetFullPath(relativeTemplateFile, Directory.GetCurrentDirectory());
                File.Copy(templateFilePath, targetFile, true);
                Logger.LogInformation($@"PowerPoint target file '{targetFile}' created from template '.\{relativeTemplateFile}'.");
            }
            catch (IOException)
            {
                Logger.LogCritical($"Cannot copy and rename the PowerPoint template 'PPTXExportPlugin_Template.pptx' from the Plugins folder to the output file '{targetFile}' in the application folder.");
                return;
            }

            Logger.LogInformation($@"Processing of output file '{targetFile}' started.");
            try
            {
                // Open the source document as read/write. 
                using (PresentationDocument presentationDocument = PresentationDocument.Open($"{targetFile}", true))
                {
                    int countItems = 0;
                    int currentItem = 0;
                    int countChanges = 0;
                    string[] itemSystemStates = { };
                    foreach (var ReportItem in ReportItems)
                    {
                        string slideTitle = ReportItem.ID + ": " + ReportItem.Title;
                        Logger.LogInformation($@"Processing of item '{slideTitle}' started.");
                        bool firstSystemRev = true;
                        bool slideChangeEmpty = true;
                        bool slideAdded = false;
                        string slideContentValue = "";
                        int itemCount = 0;
                        foreach (var ChangedField in ReportItem.ChangedFields)
                        {
                            itemCount++;
                            if (ChangedField.Key == "System.Rev" && !firstSystemRev)
                            {
                                if (!slideChangeEmpty)
                                {
                                    // Update count of changed items and amount of changes
                                    if (currentItem != ReportItem.ID)
                                    {
                                        currentItem = ReportItem.ID;
                                        countItems++;
                                    }
                                    countChanges++;
                                    // Generate detail slide for single ADO item change.
                                    Logger.LogInformation($@"Adding slide '{slideTitle}'.");
                                    InsertNewSlideWithText(presentationDocument, slideTitle, "SingleChange", new string[,] { { "Content1", slideContentValue } });
                                    slideAdded = true;
                                    slideChangeEmpty = true;
                                }
                                else
                                {
                                    Logger.LogInformation($@"Adding slide skipped. No useful data found in change.");
                                }
                                slideContentValue = "";
                            }
                            else
                            {
                                // Check if ChangedField for the current ADO item change contains real changes to avoid generating slides for ADO item saves without actual changes.
                                if (ChangedField.Key != "System.Rev" && ChangedField.Key != "System.ChangedDate" && ChangedField.Key != "System.ChangedBy")
                                {
                                    slideChangeEmpty = false;
                                }
                                //
                                if (slideContentValue == "")
                                {
                                    slideContentValue = "URL" + Environment.NewLine + ReportItem.EngineeringWorkItemURL + Environment.NewLine + Environment.NewLine;
                                }
                                // Add ChangedField key and value to output variable for the current ADO item change.
                                slideContentValue = slideContentValue + ChangedField.Key.Replace("System.", "", StringComparison.InvariantCultureIgnoreCase) + Environment.NewLine + ReformatHTML(ChangedField.CurrentValue.ToString()) + Environment.NewLine + Environment.NewLine;
                            }

                            if (ChangedField.Key == "System.Rev" && firstSystemRev)
                            {
                                firstSystemRev = false;
                            }

                            // Check if last changed field for this report item was processed and if so generate slide
                            if (ReportItem.ChangedFields.Count == itemCount)
                            {
                                if (!slideChangeEmpty)
                                {
                                    // Update count of changed items and amount of changes
                                    if (currentItem != ReportItem.ID)
                                    {
                                        currentItem = ReportItem.ID;
                                        countItems++;
                                    }
                                    countChanges++;
                                    // Generate detail slide for single ADO item change.
                                    Logger.LogInformation($@"Adding slide '{slideTitle}'.");
                                    InsertNewSlideWithText(presentationDocument, slideTitle, "SingleChange", new string[,] { { "Content1", slideContentValue } });
                                    slideAdded = true;
                                }
                                else
                                {
                                    Logger.LogInformation($@"Adding slide skipped. No useful data found in change.");
                                }

                                // If a slide was added for this ADO item we need to store the basic information for the overview slide.
                                if (slideAdded)
                                {
                                    Array.Resize(ref itemSystemStates, itemSystemStates.Length + 1);
                                    itemSystemStates[itemSystemStates.Length - 1] = ReportItem.CurrentItemFields.First(CurrentItemField => CurrentItemField.Key == "System.State").Value.ToString();
                                }
                            }
                        }
                        Logger.LogInformation($@"Processing of item '{slideTitle}' finished.");
                    }

                    // Create a dictionary for array itemSystemStates to store distinct elements and count the frequency.
                    var distinctSystemStates = new Dictionary<string, int>();

                    // Loop through the array and update the dictionary.
                    foreach (var itemSystemState in itemSystemStates)
                    {
                        // If the element is already in the dictionary, increment its count.
                        if (distinctSystemStates.ContainsKey(itemSystemState))
                        {
                            distinctSystemStates[itemSystemState]++;
                        }
                        // Otherwise, add the element with a count of 1.
                        else
                        {
                            distinctSystemStates[itemSystemState] = 1;
                        }
                    }

                    Logger.LogInformation($@"Adding slide 'Overview'.");

                    string[,] slideContent = new string[(2 + (distinctSystemStates.Count * 2)), 2];

                    slideContent[0, 0] = "Items_Content";
                    slideContent[0, 1] = countItems.ToString();
                    slideContent[1, 0] = "Changes_Content";
                    slideContent[1, 1] = countChanges.ToString();

                    int slideContentPosition = 2;
                    int slidePlaceholderPosition = 1;
                    foreach (var distinctSystemState in distinctSystemStates)
                    {

                        slideContent[slideContentPosition, 0] = "State_Type_" + slidePlaceholderPosition.ToString();
                        slideContent[slideContentPosition, 1] = distinctSystemState.Key;

                        slideContent[(slideContentPosition + 1), 0] = "State_Count_" + slidePlaceholderPosition.ToString();
                        slideContent[(slideContentPosition + 1), 1] = distinctSystemState.Value.ToString();

                        slideContentPosition += 2;
                        slidePlaceholderPosition++;
                    }

                    InsertNewSlideWithText(presentationDocument, "Overview", "Overview", slideContent, 1);

                    //InsertNewSlideWithText(presentationDocument, "Overview", "Overview", new string[,] { { "Items_Content", countItems.ToString() }, { "Changes_Content", countChanges.ToString() } }, 1);
                }
            }
            catch (IOException)
            {
                Logger.LogCritical($"Cannot access the output file '{targetFile}' in the application folder.");
                return;
            }
            Logger.LogInformation($@"Processing of output file '{targetFile}' finished.");
        }

        // Insert the specified slide into the presentation at the specified position.
        internal void InsertNewSlideWithText(PresentationDocument presentationDocument, string slideTitle, string slideLayout, string[,] slideContent, int? slidePosition = null)
        {
            PresentationPart? presentationPart = presentationDocument.PresentationPart;

            // Verify that the presentation is not empty.
            if (presentationPart is null)
            {
                Logger.LogCritical("The provided object for the presentation document is empty.");
                return;
            }

            // Declare and instantiate a new slide.
            Slide slide = new Slide(new CommonSlideData(new ShapeTree()));
            uint drawingObjectId = 1;

            // Construct the slide content.            
            // Specify the non-visual properties of the new slide.
            CommonSlideData commonSlideData = slide.CommonSlideData ?? slide.AppendChild(new CommonSlideData());
            ShapeTree shapeTree = commonSlideData.ShapeTree ?? commonSlideData.AppendChild(new ShapeTree());
            NonVisualGroupShapeProperties nonVisualProperties = shapeTree.AppendChild(new NonVisualGroupShapeProperties());
            nonVisualProperties.NonVisualDrawingProperties = new NonVisualDrawingProperties() { Id = 1, Name = "" };
            nonVisualProperties.NonVisualGroupShapeDrawingProperties = new NonVisualGroupShapeDrawingProperties();
            nonVisualProperties.ApplicationNonVisualDrawingProperties = new ApplicationNonVisualDrawingProperties();

            // Create the slide part for the new slide.
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            // Select master slide layout and add to slide.
            SlideMasterPart slideMasterPart = presentationPart.SlideMasterParts.First();
            SlideLayoutPart slideLayoutPart = slideMasterPart.SlideLayoutParts.SingleOrDefault(sl => sl.SlideLayout.CommonSlideData.Name.Value.Equals(slideLayout, StringComparison.OrdinalIgnoreCase));
            slidePart.AddPart(slideLayoutPart);

            //// Save the new slide part.
            //slide.Save(slidePart);

            // Specify the group shape properties of the new slide.
            shapeTree.AppendChild(new GroupShapeProperties());

            // Declare and instantiate the title shape of the new slide.
            Shape titleShape = shapeTree.AppendChild(new Shape());

            drawingObjectId++;

            // Specify the required shape properties for the title shape. 
            titleShape.NonVisualShapeProperties = new NonVisualShapeProperties
                (new NonVisualDrawingProperties() { Id = drawingObjectId, Name = "Title" },
                new NonVisualShapeDrawingProperties(new Drawing.ShapeLocks() { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape() { Type = PlaceholderValues.Title }));
            titleShape.ShapeProperties = new ShapeProperties();

            // Specify the text of the title shape.
            titleShape.TextBody = new TextBody(new Drawing.BodyProperties(),
                    new Drawing.ListStyle(),
                    new Drawing.Paragraph(new Drawing.Run(new Drawing.Text() { Text = slideTitle })));

            for (int slideContentPosition = 0; slideContentPosition < slideContent.GetLength(0); slideContentPosition++)
            {
                // Find content placeholder by name and add text to it.
                Shape shape = null;
                try
                {
                    // Find placeholder index by name
                    ShapeTree shapeTree2 = slideLayoutPart.SlideLayout.CommonSlideData.ShapeTree;
                    shape = shapeTree2.Descendants<Shape>().First(s => s.NonVisualShapeProperties.NonVisualDrawingProperties.Name.Value == slideContent[slideContentPosition, 0]);
                }
                catch
                {
                    shape = null;
                    Logger.LogWarning($@"Placeholder '{slideContent[slideContentPosition, 0]}' not found.");
                }
                if (shape != null)
                {
                    // Declare and instantiate the body shape of the new slide.
                    Shape bodyShape = shapeTree.AppendChild(new Shape());
                    drawingObjectId++;

                    // Specify the required shape properties for the body shape.
                    try
                    {
                        bodyShape.NonVisualShapeProperties = new NonVisualShapeProperties(new NonVisualDrawingProperties() { Id = drawingObjectId, Name = slideContent[slideContentPosition, 0] },
                                new NonVisualShapeDrawingProperties(new Drawing.ShapeLocks() { NoGrouping = true }),
                                new ApplicationNonVisualDrawingProperties(new PlaceholderShape() { Index = shape.NonVisualShapeProperties.ApplicationNonVisualDrawingProperties.PlaceholderShape.Index }));
                        bodyShape.ShapeProperties = new ShapeProperties();

                        // Specify the text of the body shape.
                        bodyShape.TextBody = new TextBody(new Drawing.BodyProperties(),
                                new Drawing.ListStyle(),
                                new Drawing.Paragraph(new Drawing.Run(new Drawing.Text() { Text = slideContent[slideContentPosition, 1] })));
                    }
                    catch (NullReferenceException)
                    {
                        shapeTree.RemoveChild(shapeTree.LastChild);
                        drawingObjectId--;
                        Logger.LogWarning($@"Object '{slideContent[slideContentPosition, 0]}' exists but is not a placeholder.");
                    }
                }
            }

            // Modify the slide ID list in the presentation part.
            // The slide ID list should not be null.
            SlideIdList? slideIdList = presentationPart.Presentation.SlideIdList;

            // Find the slide position or insert last if position is not defined.
            uint maxSlideId = 1;
            SlideId? prevSlideId = null;
            OpenXmlElementList slideIds = slideIdList?.ChildElements ?? default;
            foreach (SlideId slideId in slideIds)
            {
                if (slideId.Id is not null && slideId.Id > maxSlideId)
                {
                    maxSlideId = slideId.Id;
                }

                if (slidePosition is not null)
                {
                    slidePosition--;
                    if (slidePosition == 0)
                    {
                        prevSlideId = slideId;
                    }
                }
                else
                {
                    prevSlideId = slideId;
                }
            }

            maxSlideId++;

            // Get the ID of the previous slide.
            SlidePart lastSlidePart;

            if (prevSlideId is not null && prevSlideId.RelationshipId is not null)
            {
                lastSlidePart = (SlidePart)presentationPart.GetPartById(prevSlideId.RelationshipId!);
            }
            else
            {
                string? firstRelId = ((SlideId)slideIds[0]).RelationshipId;
                // If the first slide does not contain a relationship ID, throw an exception.
                if (firstRelId is null)
                {
                    throw new ArgumentNullException(nameof(firstRelId));
                }

                lastSlidePart = (SlidePart)presentationPart.GetPartById(firstRelId);
            }

            // Save the new slide part.
            slide.Save(slidePart);

            // Insert the new slide into the slide list after the previous slide.
            SlideId newSlideId = slideIdList!.InsertAfter(new SlideId(), prevSlideId);
            newSlideId.Id = maxSlideId;
            newSlideId.RelationshipId = presentationPart.GetIdOfPart(slidePart);

            // Save the modified presentation.
            presentationPart.Presentation.Save();
        }

    }
}
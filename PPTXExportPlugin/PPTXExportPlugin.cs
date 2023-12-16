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

namespace PPTXExportPlugin
{
    public class PPTXExportPlugin : IPlugin
    {
        public string Name { get => "PPTX Export Plugin"; }
        public string Description { get => "Exports changes to an PPTX file."; }
        public ILogger Logger { get; set; }

        public static string[] IgnoreFieldsStartingWith { get; set; } = new[] {
            "System.BoardColumn", "Microsoft.VSTS.", "WEF_"
        };

        public int Execute(List<IReportItem> items)
        {
            if (Logger is null) throw new ArgumentNullException(nameof(Logger));

            Console.WriteLine("Started.");
            CreatePPTX(items);
            Console.WriteLine("Finished.");
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

        internal void CreatePPTX(List<IReportItem> workItems)
        {
            string source;
            try
            {
            var templatePath = Path.Combine(new[] { "Plugins", "PPTXExportPlugin_Template.pptx" });
            source = File.ReadAllText(templatePath);
            }
            catch (FileNotFoundException)
            {
            Logger.LogInformation("Cannot find the PPTX template 'PPTXExportPlugin_Template.pptx' in the Plugins folder. Please make sure it is there and try again.");
            return;
            }

            string filename = $"{Environment.GetEnvironmentVariable("ORGANIZATION")}-{Environment.GetEnvironmentVariable("PROJECT")}-{DateTime.Now.ToShortDateString().Replace("/", "-")}-Export";

            var data = new
            {
            title = filename,
            ReportItems = FilterItems(workItems)
            };
            InsertNewSlide(@"Plugins\PPTXExportPlugin_Template.pptx", 1, "My new slide");
            Logger.LogInformation($"PPTX file written to {filename}.pptx");
        }

        // Insert a slide into the specified presentation.
        static void InsertNewSlide(string presentationFile, int position, string slideTitle)
        {
            // Open the source document as read/write. 
            using (PresentationDocument presentationDocument = PresentationDocument.Open(presentationFile, true))
            {
                // Pass the source document and the position and title of the slide to be inserted to the next method.
                InsertNewSlideFromPresentation(presentationDocument, position, slideTitle);
            }
        }

        // Insert the specified slide into the presentation at the specified position.
        static void InsertNewSlideFromPresentation(PresentationDocument presentationDocument, int position, string slideTitle)
        {
            PresentationPart? presentationPart = presentationDocument.PresentationPart;

            // Verify that the presentation is not empty.
            if (presentationPart is null)
            {
                throw new InvalidOperationException("The presentation document is empty.");
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
            SlideLayoutPart slideLayoutPart = slideMasterPart.SlideLayoutParts.SingleOrDefault(sl => sl.SlideLayout.CommonSlideData.Name.Value.Equals("Test", StringComparison.OrdinalIgnoreCase));
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
                    new Drawing.Paragraph(new Drawing.Run(new Drawing.Text() { Text = "Title Text" })));

            // Declare and instantiate the body shape of the new slide.
            Shape bodyShape = shapeTree.AppendChild(new Shape());
            drawingObjectId++;

            // Find placeholder index by name
            UInt32Value slideTextContentPlaceholderIndex = null;
            ShapeTree shapeTree2 = slideLayoutPart.SlideLayout.CommonSlideData.ShapeTree;
            Shape shape = shapeTree2.Descendants<Shape>().FirstOrDefault(s => s.NonVisualShapeProperties.NonVisualDrawingProperties.Name.Value == "textfield2");
            if (shape != null)
            {
                slideTextContentPlaceholderIndex = shape.NonVisualShapeProperties.ApplicationNonVisualDrawingProperties.PlaceholderShape.Index;
                Console.WriteLine(slideTextContentPlaceholderIndex.ToString());
            }

            // Specify the required shape properties for the body shape.
            bodyShape.NonVisualShapeProperties = new NonVisualShapeProperties(new NonVisualDrawingProperties() { Id = drawingObjectId, Name = "Content Placeholder" },
                    new NonVisualShapeDrawingProperties(new Drawing.ShapeLocks() { NoGrouping = true }),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape() { Index = slideTextContentPlaceholderIndex }));
            bodyShape.ShapeProperties = new ShapeProperties();

            // Specify the text of the body shape.
            bodyShape.TextBody = new TextBody(new Drawing.BodyProperties(),
                    new Drawing.ListStyle(),
                    new Drawing.Paragraph(new Drawing.Run(new Drawing.Text() { Text = "XXX Text XXX" })));

            // Modify the slide ID list in the presentation part.
            // The slide ID list should not be null.
            SlideIdList? slideIdList = presentationPart.Presentation.SlideIdList;

            // Find the highest slide ID in the current list.
            uint maxSlideId = 1;
            SlideId? prevSlideId = null;

            OpenXmlElementList slideIds = slideIdList?.ChildElements ?? default;

            foreach (SlideId slideId in slideIds)
            {
                if (slideId.Id is not null && slideId.Id > maxSlideId)
                {
                    maxSlideId = slideId.Id;
                }

                position--;
                if (position == 0)
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

            //// Create the slide part for the new slide.
            //SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();

            //// Select master slide layout and add to slide.
            //SlideMasterPart slideMasterPart = presentationPart.SlideMasterParts.First();
            //SlideLayoutPart slideLayoutPart = slideMasterPart.SlideLayoutParts.SingleOrDefault(sl => sl.SlideLayout.CommonSlideData.Name.Value.Equals("Test", StringComparison.OrdinalIgnoreCase));
            //slidePart.AddPart(slideLayoutPart);

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
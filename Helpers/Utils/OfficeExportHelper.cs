using System.Drawing;
using System.Drawing.Imaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using P = DocumentFormat.OpenXml.Presentation;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace SpeedScanManager;

/// <summary>
/// "Nach Word / Excel / PowerPoint" and "In Bilderordner": builds Office documents and
/// JPEG files from the processed scan pages. Images are embedded as JPEG (1 bpp pages as
/// PNG, which is lossless and smaller) with their real size derived from pixel size and
/// resolution. All methods are thread-agnostic (run them on a worker thread); the packages
/// are closed before the methods return. The bitmaps are not disposed.
/// </summary>
internal static class OfficeExportHelper
{
    private const long EmuPerInch = 914400;
    private const float FallbackDpi = 200f;

    // A4 in EMU (210 x 297 mm)
    private const long A4WidthEmu = 7560000;
    private const long A4HeightEmu = 10692000;

    private sealed class EncodedImage
    {
        public byte[] Data = Array.Empty<byte>();
        public PartTypeInfo PartType;
        public long WidthEmu;
        public long HeightEmu;
    }

    private static EncodedImage Encode(Bitmap bmp, int jpegQuality)
    {
        float dpiX = bmp.HorizontalResolution > 1 ? bmp.HorizontalResolution : FallbackDpi;
        float dpiY = bmp.VerticalResolution > 1 ? bmp.VerticalResolution : FallbackDpi;
        var result = new EncodedImage
        {
            WidthEmu = (long)Math.Round(bmp.Width / dpiX * EmuPerInch),
            HeightEmu = (long)Math.Round(bmp.Height / dpiY * EmuPerInch)
        };

        using var ms = new MemoryStream();
        if (bmp.PixelFormat == PixelFormat.Format1bppIndexed)
        {
            bmp.Save(ms, ImageFormat.Png);
            result.PartType = ImagePartType.Png;
        }
        else
        {
            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Math.Clamp(jpegQuality, 1, 100));
            bmp.Save(ms, ScanOutputProcessor.GetJpegEncoder(), ep);
            result.PartType = ImagePartType.Jpeg;
        }
        result.Data = ms.ToArray();
        return result;
    }

    /// <summary>Scales (w, h) down proportionally so it fits into (maxW, maxH). Never enlarges.</summary>
    private static (long W, long H) Fit(long w, long h, long maxW, long maxH)
    {
        if (w <= 0 || h <= 0) return (maxW, maxH);
        double scale = Math.Min(1.0, Math.Min((double)maxW / w, (double)maxH / h));
        return ((long)(w * scale), (long)(h * scale));
    }

    /// <summary>Scales (w, h) proportionally (up or down) so it fills (maxW, maxH).</summary>
    private static (long W, long H) FitInto(long w, long h, long maxW, long maxH)
    {
        if (w <= 0 || h <= 0) return (maxW, maxH);
        double scale = Math.Min((double)maxW / w, (double)maxH / h);
        return ((long)(w * scale), (long)(h * scale));
    }

    // ───────────────────────── Word ─────────────────────────

    public static void CreateWord(IReadOnlyList<Bitmap> pages, string path, int jpegQuality)
    {
        // A4 portrait, 1.27 cm margins (twips: 1 inch = 1440)
        const int pageW = 11906, pageH = 16838, margin = 720;
        long maxW = (pageW - 2 * margin) * EmuPerInch / 1440;
        long maxH = (pageH - 2 * margin) * EmuPerInch / 1440 - 120000; // leave room for the paragraph

        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new W.Body();

            for (int i = 0; i < pages.Count; i++)
            {
                var img = Encode(pages[i], jpegQuality);
                var imagePart = mainPart.AddImagePart(img.PartType);
                using (var ms = new MemoryStream(img.Data))
                    imagePart.FeedData(ms);
                var relId = mainPart.GetIdOfPart(imagePart);

                var (cx, cy) = Fit(img.WidthEmu, img.HeightEmu, maxW, maxH);
                var paragraph = new W.Paragraph();
                if (i > 0)
                    paragraph.Append(new W.ParagraphProperties(new W.PageBreakBefore()));
                paragraph.Append(new W.Run(new W.Drawing(CreateInline(relId, (uint)(i + 1), cx, cy))));
                body.Append(paragraph);
            }

            body.Append(new W.SectionProperties(
                new W.PageSize { Width = (UInt32Value)(uint)pageW, Height = (UInt32Value)(uint)pageH },
                new W.PageMargin
                {
                    Top = margin, Bottom = margin,
                    Left = (UInt32Value)(uint)margin, Right = (UInt32Value)(uint)margin,
                    Header = (UInt32Value)360U, Footer = (UInt32Value)360U, Gutter = (UInt32Value)0U
                }));

            mainPart.Document = new W.Document(body);
            mainPart.Document.Save();
        }
    }

    private static DW.Inline CreateInline(string relId, uint id, long cx, long cy)
    {
        return new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = id, Name = $"Scan {id}" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"Scan{id}" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };
    }

    // ───────────────────────── Excel ─────────────────────────

    public static void CreateExcel(IReadOnlyList<Bitmap> pages, string path, int jpegQuality)
    {
        using (var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
        {
            var wbPart = doc.AddWorkbookPart();
            wbPart.Workbook = new S.Workbook();
            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            var drawingsPart = wsPart.AddNewPart<DrawingsPart>();

            wsPart.Worksheet = new S.Worksheet(
                new S.SheetData(),
                new S.Drawing { Id = wsPart.GetIdOfPart(drawingsPart) });

            wbPart.Workbook.AppendChild(new S.Sheets(new S.Sheet
            {
                Id = wbPart.GetIdOfPart(wsPart),
                SheetId = 1,
                Name = "Scan"
            }));

            var wsDrawing = new Xdr.WorksheetDrawing();
            wsDrawing.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
            wsDrawing.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            const long gap = 360000; // ~1 cm between pages
            long y = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                var img = Encode(pages[i], jpegQuality);
                var imagePart = drawingsPart.AddImagePart(img.PartType);
                using (var ms = new MemoryStream(img.Data))
                    imagePart.FeedData(ms);
                var relId = drawingsPart.GetIdOfPart(imagePart);
                uint id = (uint)(i + 2);

                var picture = new Xdr.Picture(
                    new Xdr.NonVisualPictureProperties(
                        new Xdr.NonVisualDrawingProperties { Id = id, Name = $"Scan {i + 1}" },
                        new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
                    new Xdr.BlipFill(
                        new A.Blip { Embed = relId },
                        new A.Stretch(new A.FillRectangle())),
                    new Xdr.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = y },
                            new A.Extents { Cx = img.WidthEmu, Cy = img.HeightEmu }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

                wsDrawing.Append(new Xdr.AbsoluteAnchor(
                    new Xdr.Position { X = 0L, Y = y },
                    new Xdr.Extent { Cx = img.WidthEmu, Cy = img.HeightEmu },
                    picture,
                    new Xdr.ClientData()));

                y += img.HeightEmu + gap;
            }

            drawingsPart.WorksheetDrawing = wsDrawing;
            drawingsPart.WorksheetDrawing.Save();
            wsPart.Worksheet.Save();
            wbPart.Workbook.Save();
        }
    }

    // ───────────────────────── PowerPoint ─────────────────────────

    public static void CreatePowerPoint(IReadOnlyList<Bitmap> pages, string path, int jpegQuality)
    {
        // Slide size: A4, orientation of the first page.
        bool landscape = pages.Count > 0 && pages[0].Width > pages[0].Height;
        long slideW = landscape ? A4HeightEmu : A4WidthEmu;
        long slideH = landscape ? A4WidthEmu : A4HeightEmu;

        using (var doc = PresentationDocument.Create(path, PresentationDocumentType.Presentation))
        {
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new P.Presentation();

            // Master, layout and theme (required for PowerPoint to open the file without repair)
            var masterPart = presPart.AddNewPart<SlideMasterPart>("rId1");
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>("rId1");
            layoutPart.SlideLayout = CreateSlideLayout();
            layoutPart.SlideLayout.Save();
            layoutPart.AddPart(masterPart, "rId1");

            masterPart.SlideMaster = CreateSlideMaster(masterPart.GetIdOfPart(layoutPart));
            var themePart = masterPart.AddNewPart<ThemePart>("rId5");
            themePart.Theme = CreateTheme();
            themePart.Theme.Save();
            presPart.AddPart(themePart, "rId5");
            masterPart.SlideMaster.Save();

            var slideIdList = new P.SlideIdList();
            uint slideId = 256;
            for (int i = 0; i < pages.Count; i++)
            {
                var img = Encode(pages[i], jpegQuality);
                var slidePart = presPart.AddNewPart<SlidePart>($"rId{10 + i}");
                slidePart.AddPart(layoutPart, "rId1");
                var imagePart = slidePart.AddImagePart(img.PartType, "rId2");
                using (var ms = new MemoryStream(img.Data))
                    imagePart.FeedData(ms);

                // Fit (up or down) into the slide, centered.
                var (cx, cy) = FitInto(img.WidthEmu, img.HeightEmu, slideW, slideH);
                long x = (slideW - cx) / 2, y = (slideH - cy) / 2;

                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(
                        new P.ShapeTree(
                            new P.NonVisualGroupShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                                new P.NonVisualGroupShapeDrawingProperties(),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.GroupShapeProperties(new A.TransformGroup()),
                            new P.Picture(
                                new P.NonVisualPictureProperties(
                                    new P.NonVisualDrawingProperties { Id = 2U, Name = $"Scan {i + 1}" },
                                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                                    new P.ApplicationNonVisualDrawingProperties()),
                                new P.BlipFill(
                                    new A.Blip { Embed = "rId2" },
                                    new A.Stretch(new A.FillRectangle())),
                                new P.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = x, Y = y },
                                        new A.Extents { Cx = cx, Cy = cy }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))),
                    new P.ColorMapOverride(new A.MasterColorMapping()));
                slidePart.Slide.Save();

                slideIdList.Append(new P.SlideId { Id = slideId++, RelationshipId = presPart.GetIdOfPart(slidePart) });
            }

            presPart.Presentation.Append(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" }),
                slideIdList,
                new P.SlideSize { Cx = (Int32Value)(int)slideW, Cy = (Int32Value)(int)slideH },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 },
                new P.DefaultTextStyle());
            presPart.Presentation.Save();
        }
    }

    private static P.SlideLayout CreateSlideLayout() => new(
        new P.CommonSlideData(
            new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup())))
        { Name = "Leer" },
        new P.ColorMapOverride(new A.MasterColorMapping()))
    { Type = P.SlideLayoutValues.Blank, Preserve = true };

    private static P.SlideMaster CreateSlideMaster(string layoutRelId) => new(
        new P.CommonSlideData(
            new P.Background(
                new P.BackgroundStyleReference(new A.SchemeColor { Val = A.SchemeColorValues.Background1 }) { Index = 1001U }),
            new P.ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.GroupShapeProperties(new A.TransformGroup()))),
        new P.ColorMap
        {
            Background1 = A.ColorSchemeIndexValues.Light1,
            Text1 = A.ColorSchemeIndexValues.Dark1,
            Background2 = A.ColorSchemeIndexValues.Light2,
            Text2 = A.ColorSchemeIndexValues.Dark2,
            Accent1 = A.ColorSchemeIndexValues.Accent1,
            Accent2 = A.ColorSchemeIndexValues.Accent2,
            Accent3 = A.ColorSchemeIndexValues.Accent3,
            Accent4 = A.ColorSchemeIndexValues.Accent4,
            Accent5 = A.ColorSchemeIndexValues.Accent5,
            Accent6 = A.ColorSchemeIndexValues.Accent6,
            Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
            FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
        },
        new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = layoutRelId }),
        new P.TextStyles(new P.TitleStyle(), new P.BodyStyle(), new P.OtherStyle()));

    private static A.Theme CreateTheme()
    {
        static A.SolidFill Scheme(A.SchemeColorValues v) => new(new A.SchemeColor { Val = v });
        static A.Outline Line(int w) => new(Scheme(A.SchemeColorValues.PhColor)) { Width = w };
        static A.EffectStyle Effect() => new(new A.EffectList());

        return new A.Theme(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                    new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" }))
                { Name = "Office" },
                new A.FontScheme(
                    new A.MajorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                    new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
                { Name = "Office" },
                new A.FormatScheme(
                    new A.FillStyleList(
                        Scheme(A.SchemeColorValues.PhColor),
                        Scheme(A.SchemeColorValues.PhColor),
                        Scheme(A.SchemeColorValues.PhColor)),
                    new A.LineStyleList(Line(9525), Line(25400), Line(38100)),
                    new A.EffectStyleList(Effect(), Effect(), Effect()),
                    new A.BackgroundFillStyleList(
                        Scheme(A.SchemeColorValues.PhColor),
                        Scheme(A.SchemeColorValues.PhColor),
                        Scheme(A.SchemeColorValues.PhColor)))
                { Name = "Office" }),
            new A.ObjectDefaults(),
            new A.ExtraColorSchemeList())
        { Name = "Office" };
    }

    // ───────────────────────── Picture folder ─────────────────────────

    /// <summary>
    /// Saves the pages as JPEG files (1 bpp pages as PNG) into <paramref name="folder"/>:
    /// "{baseName}.jpg" for one page, "{baseName}_001.jpg", ... otherwise; never overwrites.
    /// </summary>
    public static List<string> SaveImages(IReadOnlyList<Bitmap> pages, string folder, string baseName, int jpegQuality)
    {
        Directory.CreateDirectory(folder);
        var files = new List<string>();
        var jpegEncoder = ScanOutputProcessor.GetJpegEncoder();
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)Math.Clamp(jpegQuality, 1, 100));

        for (int i = 0; i < pages.Count; i++)
        {
            var bmp = pages[i];
            bool png = bmp.PixelFormat == PixelFormat.Format1bppIndexed;
            string name = pages.Count == 1 ? baseName : $"{baseName}_{i + 1:D3}";
            string path = ScanOutputProcessor.MakeUniquePath(folder, name + (png ? ".png" : ".jpg"));
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                if (png) bmp.Save(fs, ImageFormat.Png);
                else bmp.Save(fs, jpegEncoder, ep);
            }
            files.Add(path);
        }
        return files;
    }
}

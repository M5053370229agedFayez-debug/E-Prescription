using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using ePrescriptions.Core.Helpers;
using ePrescriptions.Core.Printing;
using ePrescriptions.Data.Repositories;
using ePrescriptions.Domain.Entities;

namespace ePrescriptions.App.Services
{
    public interface IBarcodeLabelService
    {
        bool PrintBarcodeLabel(Item item, ItemBatch? batch, int labelCount, out string errorMessage);
    }

    public class BarcodeLabelService : IBarcodeLabelService
    {
        private readonly SettingsRepository _settingsRepository;

        public BarcodeLabelService(SettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        /// <summary>يدور في مقاسات ورق الطابعة على مقاس مطابق للملصق (فرق أقصاه 8 وحدات ≈ 2 مم) وإلا يرجع لمقاس مخصص.</summary>
        private static PaperSize ResolvePaperSize(PrinterSettings printerSettings, int widthUnits, int heightUnits)
        {
            try
            {
                foreach (PaperSize size in printerSettings.PaperSizes)
                {
                    if (Math.Abs(size.Width - widthUnits) <= 8 && Math.Abs(size.Height - heightUnits) <= 8)
                        return size;
                }
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"تعذر قراءة مقاسات ورق الطابعة، هيتم استخدام مقاس مخصص: {ex.Message}", "BarcodeLabelService");
            }
            return new PaperSize("CustomLabel", widthUnits, heightUnits);
        }

        /// <summary>
        /// أكبر خط (من startPt لتحت لحد minPt) يخلّي النص يتسع في العرض. اللي بيستدعي هو اللي بيعمل Dispose للخط.
        /// </summary>
        private static Font FitFont(Graphics g, string text, string family, float startPt, float minPt, float maxWidth, FontStyle style)
        {
            float pt = startPt;
            while (pt > minPt)
            {
                var candidate = new Font(family, pt, style);
                if (g.MeasureString(text, candidate).Width <= maxWidth) return candidate;
                candidate.Dispose();
                pt -= 0.25f;
            }
            return new Font(family, minPt, style);
        }

        private static StringFormat MakeFormat(StringAlignment alignment, bool rtl)
        {
            var sf = new StringFormat
            {
                Alignment = alignment,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            if (rtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            // أرقام لاتينية (0123) بدل الهندية (٠١٢٣) مهما كانت لغة ويندوز
            sf.SetDigitSubstitution(1033, StringDigitSubstitute.None);
            return sf;
        }

        /// <summary>طباعة الاستيكر الصغير: كل صفحة بتشيل StackCount ورقة، والأخيرة ممكن تبقى ورقة واحدة بس.</summary>
        private static void PrintMini(MiniLabelLayout mini, string printerName, string pharmacyName, string headerLeft,
            string barcode, string itemName, bool itemNameIsArabic, int labelCount)
        {
            using var printDoc = new PrintDocument();
            if (!string.IsNullOrEmpty(printerName))
                printDoc.PrinterSettings.PrinterName = printerName;

            printDoc.DefaultPageSettings.PaperSize = ResolvePaperSize(printDoc.PrinterSettings, mini.PageWidth, mini.PageHeight);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.OriginAtMargins = false;
            printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

            int remaining = labelCount;
            printDoc.PrintPage += (s, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;

                // نقطة الأصل بتبدأ بعد الهامش الصلب للطابعة؛ بنرجّعها لحافة الورقة
                g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);

                for (int slot = 0; slot < mini.StackCount && remaining > 0; slot++)
                {
                    MiniLabelRenderer.DrawLabel(g, mini, slot, pharmacyName, headerLeft, barcode, itemName, itemNameIsArabic);
                    remaining--;
                }
                e.HasMorePages = remaining > 0;
            };

            printDoc.Print();
        }

        public bool PrintBarcodeLabel(Item item, ItemBatch? batch, int labelCount, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (labelCount <= 0) labelCount = 1;

            try
            {
                var settings = _settingsRepository.GetSettings();
                string printerName = string.IsNullOrWhiteSpace(settings.LabelPrinterName) || settings.LabelPrinterName.Contains("الافتراضية")
                    ? string.Empty
                    : settings.LabelPrinterName;

                // لا اسم شخصي افتراضي: لو اسم الصيدلية فاضي سطر الاسم بيتشال
                string pharmacyName = string.IsNullOrWhiteSpace(settings.PharmacyNameAr) ? string.Empty : settings.PharmacyNameAr.Trim();
                string phone = string.IsNullOrWhiteSpace(settings.Phone) ? string.Empty : settings.Phone.Trim();

                // ملصق تشغيلة: الباركود يعرّف التشغيلة (ومنها تاريخ الصلاحية)، وغير ذلك باركود الصنف العادي
                bool isBatchLabel = batch != null && batch.Id > 0;
                string barcodeToPrint = isBatchLabel
                    ? BatchLabelCode.Encode(batch!.Id)
                    : (string.IsNullOrWhiteSpace(item.Barcode) ? item.Code : item.Barcode);

                // السطر العلوي: اسم الصيدلية يمين، والتليفون يسار (وعلى ملصق التشغيلة بيتبدل بتاريخ الصلاحية)
                string headerLeft = isBatchLabel
                    ? "EXP " + batch!.ExpiryDate.ToString("MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
                    : phone;

                // اسم الصنف تحت الباركود: الاسم الإنجليزي لو موجود (زي الملصق المرجعي) وإلا العربي
                string itemName = !string.IsNullOrWhiteSpace(item.NameEn) ? item.NameEn.Trim() : (item.NameAr ?? string.Empty).Trim();
                bool itemNameIsArabic = string.IsNullOrWhiteSpace(item.NameEn);

                // استيكر صغير (أقل من 12 مم ارتفاع، زي 30×7): الصفحة = ورقتين لاصقين فوق بعض، وكل ورقة بتتقص على حدودها
                if (MiniLabelLayout.IsMini(settings.LabelHeightMm))
                {
                    var mini = MiniLabelLayout.Compute(settings.LabelWidthMm, settings.LabelHeightMm);
                    if (!mini.Fits)
                        Logger.LogInfo($"ارتفاع الاستيكر {settings.LabelHeightMm} مم صغير جداً؛ الباركود هيطلع بأدنى ارتفاع.", "BarcodeLabelService");
                    string miniPhone = isBatchLabel ? headerLeft : MiniLabelRenderer.ToArabicIndicDigits(phone);
                    PrintMini(mini, printerName, pharmacyName, miniPhone, barcodeToPrint, itemName, itemNameIsArabic, labelCount);
                    Logger.LogInfo($"تمت طباعة {labelCount} ملصق باركود (استيكر صغير) للصنف {item.Code} بنجاح.", "BarcodeLabelService");
                    return true;
                }

                var layout = ShelfLabelLayout.Compute(settings.LabelWidthMm, settings.LabelHeightMm);
                if (!layout.Fits)
                    Logger.LogInfo($"مقاس الملصق {settings.LabelWidthMm}×{settings.LabelHeightMm} مم صغير على التوزيع؛ الباركود هيطلع بأدنى ارتفاع.", "BarcodeLabelService");

                // طباعة مهمة واحدة لكل الملصقات (صفحة لكل ملصق) بدل مهمة طباعة منفصلة لكل ملصق
                using var printDoc = new PrintDocument();
                if (!string.IsNullOrEmpty(printerName))
                {
                    printDoc.PrinterSettings.PrinterName = printerName;
                }

                // لو التعريف فيه مقاس ورق مطابق للملصق (±2 مم) نستخدمه بنفسه؛ التعريفات الحرارية كتير بتتجاهل المقاس المخصص
                // وتطبع على مقاسها الافتراضي، وده بيخلي المحتوى يتقسم على ملصقين ويتزحزح.
                printDoc.DefaultPageSettings.PaperSize = ResolvePaperSize(printDoc.PrinterSettings, layout.PageWidth, layout.PageHeight);
                printDoc.DefaultPageSettings.Landscape = false;
                printDoc.OriginAtMargins = false;
                printDoc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                int pagesPrinted = 0;
                printDoc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    if (g == null) return;

                    // نقطة الأصل في GDI+ بتبدأ من بداية المنطقة القابلة للطباعة (بعد الهامش الصلب للطابعة) مش من حافة الملصق.
                    g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);

                    int totalWidth = e.PageBounds.Width > 0 ? e.PageBounds.Width : layout.PageWidth;
                    float pad = 4f;
                    float innerWidth = Math.Max(40f, totalWidth - pad * 2);

                    using var brush = new SolidBrush(Color.Black);
                    using var sfRight = MakeFormat(StringAlignment.Near, true);    // عربي: Near = اليمين
                    using var sfLeft = MakeFormat(StringAlignment.Near, false);    // أرقام/إنجليزي: Near = اليسار
                    using var sfCenterAr = MakeFormat(StringAlignment.Center, true);
                    using var sfCenterEn = MakeFormat(StringAlignment.Center, false);

                    // 1) السطر العلوي: اسم الصيدلية (يمين) + التليفون/الصلاحية (يسار)
                    if (pharmacyName.Length > 0 || headerLeft.Length > 0)
                    {
                        float rightW = headerLeft.Length > 0 ? innerWidth * 0.62f : innerWidth;
                        float leftW = innerWidth - rightW;
                        using var fontHeader = FitFont(g, pharmacyName, "Tahoma", layout.HeaderPt, 4.5f, rightW - 2f, FontStyle.Bold);
                        using var fontHeaderLeft = new Font("Tahoma", layout.HeaderPt, FontStyle.Bold);
                        if (pharmacyName.Length > 0)
                            g.DrawString(pharmacyName, fontHeader, brush, new RectangleF(pad + leftW, layout.HeaderY, rightW, layout.HeaderH), sfRight);
                        if (headerLeft.Length > 0)
                            g.DrawString(headerLeft, fontHeaderLeft, brush, new RectangleF(pad, layout.HeaderY, leftW, layout.HeaderH), sfLeft);
                    }

                    // 2) رسم الباركود Code128 (Set C للأرقام) — أعمدة بس، من غير أرقام
                    try
                    {
                        if (!layout.BarcodeFitsWidth(Code128Encoder.TotalModules(barcodeToPrint)))
                            throw new InvalidOperationException("الباركود أعرض من الملصق");

                        using var barcodeBmp = Code128BarcodeGenerator.GenerateCode128Bitmap(
                            barcodeToPrint, height: layout.BarsHeightPx, moduleWidth: 1, includeText: false,
                            quietZone: ShelfLabelLayout.QuietZonePx, verticalPadding: 0);
                        int bmpWidthUnits = LabelLayout.PixelsToUnits(barcodeBmp.Width);
                        int bmpX = Math.Max(0, (totalWidth - bmpWidthUnits) / 2);

                        // من غير تنعيم: الأعمدة بتفضل حادة على طابعة 203 DPI
                        var oldInterpolation = g.InterpolationMode;
                        var oldPixelOffset = g.PixelOffsetMode;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(barcodeBmp, new Rectangle(bmpX, layout.BarcodeY, bmpWidthUnits, layout.BarcodeHeightUnits));
                        g.InterpolationMode = oldInterpolation;
                        g.PixelOffsetMode = oldPixelOffset;
                    }
                    catch (Exception barcodeEx)
                    {
                        Logger.LogInfo($"تعذر رسم الباركود، طُبع النص بدلاً منه: {barcodeEx.Message}", "BarcodeLabelService");
                        using var fontFallback = new Font("Tahoma", layout.NamePt, FontStyle.Bold);
                        g.DrawString(barcodeToPrint, fontFallback, brush, new RectangleF(pad, layout.BarcodeY, innerWidth, layout.BarcodeHeightUnits), sfCenterEn);
                    }

                    // 3) اسم الصنف تحت الباركود (بيصغّر الخط لو طويل بدل ما يتقص)
                    if (itemName.Length > 0)
                    {
                        using var fontName = FitFont(g, itemName, "Tahoma", layout.NamePt, 4.5f, innerWidth - 2f, FontStyle.Bold);
                        g.DrawString(itemName, fontName, brush, new RectangleF(pad, layout.NameY, innerWidth, layout.NameH),
                            itemNameIsArabic ? sfCenterAr : sfCenterEn);
                    }

                    pagesPrinted++;
                    e.HasMorePages = pagesPrinted < labelCount;
                };

                printDoc.Print();

                Logger.LogInfo($"تمت طباعة {labelCount} ملصق باركود للصنف {item.Code} بنجاح.", "BarcodeLabelService");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Logger.LogException(ex, "BarcodeLabelService.PrintBarcodeLabel");
                return false;
            }
        }
    }
}

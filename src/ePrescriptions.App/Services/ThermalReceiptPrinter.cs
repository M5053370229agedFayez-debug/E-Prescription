using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using ePrescriptions.Core.Financial;
using ePrescriptions.Core.Helpers;
using ePrescriptions.Core.Printing;
using ePrescriptions.Domain.Entities;
using ePrescriptions.Domain.Enums;
using ePrescriptions.Domain.Helpers;
using QRCoder;

namespace ePrescriptions.App.Services
{
    /// <summary>
    /// محرك طباعة الإيصالات الحرارية (58 مم / 80 مم) المتوافق مع طابعات إيصالات نقاط البيع في مصر
    /// يدعم الاتجاه العربي من اليمين لليسار، رمز الاستجابة السريع البسيط (اختياري حسب الإعدادات)، والعملة بالجنيه المصري (ج.م).
    /// </summary>
    public static class ThermalReceiptPrinter
    {
        public static bool PrintReceipt(Invoice invoice, PharmacySettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var printDoc = new PrintDocument();
                string printerName = string.IsNullOrWhiteSpace(settings.ReceiptPrinterName)
                    ? printDoc.PrinterSettings.PrinterName
                    : settings.ReceiptPrinterName;

                printDoc.PrinterSettings.PrinterName = printerName;
                if (!printDoc.PrinterSettings.IsValid)
                {
                    errorMessage = $"طابعة الإيصالات المحددة ({printerName}) غير متوفرة أو غير متصلة.";
                    return false;
                }

                int paperWidthHundredths = (int)Math.Round(settings.ReceiptPaperWidthMm * 100.0 / 25.4);
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("Receipt", paperWidthHundredths, 2000);
                printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 20);

                // هامش أمان جانبي أيمن (38 وحدة ~ 9.6 مم) لمنع قص أول حرفين من اليمين في الطابعات الحرارية ذات الرأس 72 مم
                float marginLeft = settings.ReceiptPaperWidthMm <= 58 ? 12f : 18f;
                float marginRight = settings.ReceiptPaperWidthMm <= 58 ? 28f : 38f;
                float marginX = marginLeft;
                float printWidth = Math.Max(100f, paperWidthHundredths - marginLeft - marginRight);

                printDoc.PrintPage += (sender, e) =>
                {
                    if (e.Graphics == null) return;
                    DrawInvoice(e.Graphics, invoice, settings, marginX, printWidth);
                    e.HasMorePages = false;
                };

                printDoc.Print();

                // فتح درج النقدية إذا كان مفعلاً وكانت الفاتورة نقدية
                if (settings.EnableCashDrawerOnPrint && (invoice.PaymentMethod == Domain.Enums.PaymentMethod.Cash
                    || invoice.Payments.Any(p => p.Method == Domain.Enums.PaymentMethod.Cash)))
                {
                    if (!OpenCashDrawer(settings, out string drawerError))
                        Logger.LogInfo($"تعذر فتح درج النقدية بعد الطباعة: {drawerError}", "ThermalReceiptPrinter");
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"حدث خطأ أثناء الطباعة: {ex.Message}";
                return false;
            }
        }

        public static bool OpenCashDrawer(PharmacySettings settings, out string errorMessage)
        {
            return OpenCashDrawer(settings.ReceiptPrinterName, settings.CashDrawerEscPosBytes, out errorMessage);
        }

        public static bool OpenCashDrawer(string printerName, string escPosBytesStr, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    using var defaultDoc = new PrintDocument();
                    printerName = defaultDoc.PrinterSettings.PrinterName;
                }
                if (!EscPosCommand.TryParse(escPosBytesStr, out byte[] pulseBytes, out errorMessage))
                    return false;
                return RawPrinterHelper.SendBytesToPrinter(printerName, pulseBytes, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool PrintTestTicket(string printerName, string pharmacyName, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var printDoc = new PrintDocument();
                if (!string.IsNullOrWhiteSpace(printerName))
                {
                    printDoc.PrinterSettings.PrinterName = printerName;
                }
                if (!printDoc.PrinterSettings.IsValid)
                {
                    errorMessage = $"الطابعة المحددة ({printerName}) غير متوفرة.";
                    return false;
                }

                printDoc.PrintPage += (s, e) =>
                {
                    if (e.Graphics == null) return;
                    using var f = new Font("Arial", 10, FontStyle.Bold);
                    e.Graphics.DrawString($"اختبار نجاح الاتصال بطابعة الكاشير\n{pharmacyName}\n{DbDate.Full(DateTime.Now)}", f, Brushes.Black, new PointF(10, 10));
                };

                printDoc.Print();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool PrintShiftReport(ShiftClosureReport report, PharmacySettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var printDoc = new PrintDocument();
                string printerName = string.IsNullOrWhiteSpace(settings.ReceiptPrinterName)
                    ? printDoc.PrinterSettings.PrinterName
                    : settings.ReceiptPrinterName;

                printDoc.PrinterSettings.PrinterName = printerName;
                if (!printDoc.PrinterSettings.IsValid)
                {
                    errorMessage = $"طابعة الإيصالات المحددة ({printerName}) غير متوفرة أو غير متصلة.";
                    return false;
                }

                int paperWidthHundredths = (int)Math.Round(settings.ReceiptPaperWidthMm * 100.0 / 25.4);
                printDoc.DefaultPageSettings.PaperSize = new PaperSize("ShiftReport", paperWidthHundredths, 2500);
                printDoc.DefaultPageSettings.Margins = new Margins(10, 10, 10, 20);

                printDoc.PrintPage += (sender, e) =>
                {
                    if (e.Graphics == null) return;
                    var g = e.Graphics;

                    float marginLeft = settings.ReceiptPaperWidthMm <= 58 ? 12f : 18f;
                    float marginRight = settings.ReceiptPaperWidthMm <= 58 ? 28f : 38f;
                    float printWidth = Math.Max(100f, paperWidthHundredths - marginLeft - marginRight);
                    float y = 10f;

                    using var fontTitle = new Font("Arial", 11, FontStyle.Bold);
                    using var fontSubTitle = new Font("Arial", 8.5f, FontStyle.Regular);
                    using var fontRegular = new Font("Arial", 8f, FontStyle.Regular);
                    using var fontBold = new Font("Arial", 8f, FontStyle.Bold);
                    using var fontLargeBold = new Font("Arial", 11f, FontStyle.Bold);

                    using var sfCenter = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.DirectionRightToLeft
                    };
                    using var sfRtl = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.DirectionRightToLeft
                    };
                    using var sfLtr = new StringFormat
                    {
                        Alignment = StringAlignment.Far,
                        LineAlignment = StringAlignment.Center
                    };

                    sfCenter.SetDigitSubstitution(1033, StringDigitSubstitute.None);
                    sfRtl.SetDigitSubstitution(1033, StringDigitSubstitute.None);
                    sfLtr.SetDigitSubstitution(1033, StringDigitSubstitute.None);

                    g.DrawString(settings.PharmacyNameAr, fontTitle, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 20), sfCenter);
                    y += 20;

                    g.DrawString("تقرير إغلاق الوردية وتسليم الصندوق", fontBold, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 18), sfCenter);
                    y += 20;

                    DrawDottedLine(g, y, marginLeft, marginLeft + printWidth);
                    y += 6;

                    void DrawRow(string label, string val, Font? fVal = null)
                    {
                        g.DrawString(label, fontRegular, Brushes.Black, new RectangleF(marginLeft, y, printWidth * 0.55f, 16), sfRtl);
                        g.DrawString(val, fVal ?? fontRegular, Brushes.Black, new RectangleF(marginLeft + printWidth * 0.55f, y, printWidth * 0.45f, 16), sfLtr);
                        y += 16;
                    }

                    DrawRow("رقم الوردية:", $"#{report.ShiftId}", fontBold);
                    DrawRow("الكاشير:", report.CashierName, fontBold);
                    DrawRow("وقت البدء:", DbDate.Full(report.StartTime));
                    if (report.EndTime.HasValue)
                        DrawRow("وقت الإغلاق:", DbDate.Full(report.EndTime.Value));
                    DrawRow("حالة الوردية:", report.Status == ShiftStatus.Closed ? "مغلقة" : "مفتوحة");

                    DrawDottedLine(g, y, marginLeft, marginLeft + printWidth);
                    y += 6;

                    g.DrawString("تفصيل المبيعات وطرق الدفع", fontBold, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 16), sfRtl);
                    y += 18;

                    DrawRow("عدد فواتير المبيعات:", report.InvoicesCount.ToString());
                    DrawRow("عدد المرتجعات:", report.ReturnsCount.ToString());
                    DrawRow("مبيعات نقدي:", $"{report.CashSalesPounds:N2} ج.م");
                    DrawRow("مبيعات شبكة / بطاقات:", $"{report.NetworkSalesPounds:N2} ج.م");
                    DrawRow("مبيعات آجل:", $"{report.OnAccountSalesPounds:N2} ج.م");
                    DrawRow("مبيعات تأمين:", $"{report.InsuranceSalesPounds:N2} ج.م");
                    DrawRow("إجمالي المبيعات:", $"{report.TotalSalesPounds:N2} ج.م", fontBold);
                    if (report.TotalReturnsPounds > 0)
                    {
                        DrawRow("المرتجعات النقدية:", $"-{report.CashReturnsPounds:N2} ج.م");
                        DrawRow("صافي المبيعات:", $"{report.NetSalesPounds:N2} ج.م", fontBold);
                    }

                    DrawDottedLine(g, y, marginLeft, marginLeft + printWidth);
                    y += 6;

                    g.DrawString("مقارنة النقدية وحركة الصندوق", fontBold, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 16), sfRtl);
                    y += 18;

                    DrawRow("الرصيد الافتتاحي:", $"{report.OpeningBalancePounds:N2} ج.م");
                    DrawRow("(+) المقبوضات النقدية:", $"{report.CashSalesPounds:N2} ج.م");
                    DrawRow("(-) المرتجعات النقدية:", $"-{report.CashReturnsPounds:N2} ج.م");
                    DrawRow("(=) النقد المتوقع بالدرج:", $"{report.ExpectedCashPounds:N2} ج.م", fontBold);
                    if (report.ActualCashPounds.HasValue)
                    {
                        DrawRow("النقد الفعلي المعدود:", $"{report.ActualCashPounds.Value:N2} ج.م", fontLargeBold);
                        string diffStr = report.DifferencePounds.HasValue
                            ? (report.DifferencePounds.Value == 0
                                ? "مطابق تماماً (0.00 ج.م)"
                                : report.DifferencePounds.Value > 0
                                    ? $"+{report.DifferencePounds.Value:N2} ج.م (زيادة)"
                                    : $"{report.DifferencePounds.Value:N2} ج.م (عجز)")
                            : "—";
                        DrawRow("الفارق في الصندوق:", diffStr, fontLargeBold);
                    }

                    if (!string.IsNullOrWhiteSpace(report.Notes))
                    {
                        y += 4;
                        g.DrawString($"ملاحظات: {report.Notes}", fontRegular, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 30), sfRtl);
                        y += 32;
                    }

                    DrawDottedLine(g, y, marginLeft, marginLeft + printWidth);
                    y += 10;

                    g.DrawString("توقيع الكاشير المسلّم: ........................", fontRegular, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 16), sfRtl);
                    y += 20;
                    g.DrawString("توقيع الإدارة / المستلم: ........................", fontRegular, Brushes.Black, new RectangleF(marginLeft, y, printWidth, 16), sfRtl);
                    y += 22;

                    g.DrawString(AppBranding.ProductByline, fontSubTitle, Brushes.Gray, new RectangleF(marginLeft, y, printWidth, 14), sfCenter);
                    y += 14;
                    g.DrawString(AppBranding.Copyright, fontRegular, Brushes.Gray, new RectangleF(marginLeft, y, printWidth, 14), sfCenter);

                    e.HasMorePages = false;
                };

                printDoc.Print();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"حدث خطأ أثناء طباعة تقرير الوردية: {ex.Message}";
                Logger.LogException(ex, "ThermalReceiptPrinter.PrintShiftReport");
                return false;
            }
        }

        private static StringFormat MakeFormat(StringAlignment alignment, bool rtl)
        {
            var sf = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center };
            if (rtl) sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            // أرقام لاتينية (0123) بدل الهندية (٠١٢٣) مهما كانت لغة ويندوز، وده كمان بيمنع قلب ترتيب الأرقام في السطور المختلطة
            sf.SetDigitSubstitution(1033, StringDigitSubstitute.None);
            return sf;
        }

        /// <summary>
        /// يرسم الفاتورة على شكل الإيصال المرجعي: صندوق الترويسة (اسم الصيدلية، التليفون، التاريخ والرقم)،
        /// جدول الأصناف بخطوط (الكمية | السعر | الخصم | الجملة)، ثم صندوق المجاميع. بيرجع آخر y مرسوم.
        /// </summary>
        private static float DrawInvoice(Graphics g, Invoice invoice, PharmacySettings settings, float x, float w)
        {
            float right = x + w;
            float y = 6f;

            using var fTiny = new Font("Arial", 6.5f, FontStyle.Regular);
            using var fSmall = new Font("Arial", 8f, FontStyle.Regular);
            using var fBold = new Font("Arial", 8f, FontStyle.Bold);
            using var fName = new Font("Arial", 13f, FontStyle.Bold);
            using var fNet = new Font("Arial", 11f, FontStyle.Bold);
            using var pen = new Pen(Color.Black, 1f);

            using var sfCenter = MakeFormat(StringAlignment.Center, true);      // عربي في النص
            using var sfRight = MakeFormat(StringAlignment.Near, true);         // عربي على اليمين
            using var sfNumCenter = MakeFormat(StringAlignment.Center, false);  // أرقام في النص
            using var sfNumLeft = MakeFormat(StringAlignment.Near, false);      // إنجليزي/أرقام على اليسار

            // ===== 1) صندوق الترويسة =====
            float headerTop = y;
            y += 3f;
            g.DrawString("Print time : " + DbDate.Minutes(DateTime.Now), fTiny, Brushes.Black, new RectangleF(x + 4, y, w - 8, 12), sfNumLeft);
            y += 14f;

            ReceiptText.SplitPharmacyName(settings.PharmacyNameAr, out string namePrefix, out string bigName);
            if (namePrefix.Length > 0)
            {
                g.DrawString(namePrefix, fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfCenter);
                y += 14f;
            }
            if (bigName.Length > 0)
            {
                float nameH = Math.Max(22f, g.MeasureString(bigName, fName, (int)(w - 8), sfCenter).Height + 2f);
                g.DrawString(bigName, fName, Brushes.Black, new RectangleF(x + 4, y, w - 8, nameH), sfCenter);
                y += nameH;
            }

            if (!string.IsNullOrWhiteSpace(settings.PharmacyNameEn))
            {
                g.DrawString(settings.PharmacyNameEn, fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfNumCenter);
                y += 14f;
            }
            if (!string.IsNullOrWhiteSpace(settings.ResponsiblePharmacist))
            {
                g.DrawString($"الصيدلي المسؤول: {settings.ResponsiblePharmacist}", fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfCenter);
                y += 14f;
            }
            if (!string.IsNullOrWhiteSpace(settings.Address))
            {
                g.DrawString(settings.Address, fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfCenter);
                y += 14f;
            }
            if (!string.IsNullOrWhiteSpace(settings.TaxNumber))
            {
                g.DrawString($"رقم التسجيل الضريبي: {settings.TaxNumber}", fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfCenter);
                y += 14f;
            }
            if (!string.IsNullOrWhiteSpace(settings.CommercialRegister))
            {
                g.DrawString($"س.ت: {settings.CommercialRegister}", fSmall, Brushes.Black, new RectangleF(x, y, w, 14), sfCenter);
                y += 14f;
            }
            if (!string.IsNullOrWhiteSpace(settings.Phone))
            {
                g.DrawString("Tel:" + settings.Phone.Trim(), fBold, Brushes.Black, new RectangleF(x, y, w, 16), sfNumCenter);
                y += 16f;
            }

            y += 2f;
            // عنوان الإيصال حسب النوع (مرتجع / آجل / تأمين ...)
            g.DrawString(ReceiptText.InvoiceTitle(invoice.Type, invoice.PaymentMethod), fBold, Brushes.Black, new RectangleF(x, y, w, 16), sfCenter);
            y += 17f;
            g.DrawString($"تاريخ الفاتورة: {DbDate.Date(invoice.Date)}   {invoice.Date.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}", fSmall, Brushes.Black, new RectangleF(x + 4, y, w - 8, 15), sfRight);
            y += 15f;
            g.DrawString($"رقم الفاتورة: {invoice.InvoiceNumber}", fBold, Brushes.Black, new RectangleF(x + 4, y, w - 8, 15), sfRight);
            y += 15f;
            if (!string.IsNullOrWhiteSpace(invoice.CustomerName) && invoice.CustomerName != "عميل نقدي")
            {
                g.DrawString($"العميل: {invoice.CustomerName}", fSmall, Brushes.Black, new RectangleF(x + 4, y, w - 8, 15), sfRight);
                y += 15f;
            }
            y += 3f;
            g.DrawRectangle(pen, x, headerTop, w, y - headerTop);

            // ===== 2) جدول الأصناف =====
            y += 6f;
            float tableTop = y;
            g.DrawString("اسم الصنف", fBold, Brushes.Black, new RectangleF(x, y, w, 16), sfCenter);
            y += 16f;
            g.DrawLine(pen, x, y, right, y);

            // الأعمدة من الشمال لليمين: الكمية | السعر | الخصم | الجملة
            float cQty = w * 0.22f, cPrice = w * 0.26f, cDisc = w * 0.20f;
            float xQty = x, xPrice = xQty + cQty, xDisc = xPrice + cPrice, xTot = xDisc + cDisc;
            float cTot = right - xTot;
            g.DrawString("الكمية", fSmall, Brushes.Black, new RectangleF(xQty, y, cQty, 15), sfCenter);
            g.DrawString("السعر", fSmall, Brushes.Black, new RectangleF(xPrice, y, cPrice, 15), sfCenter);
            g.DrawString("خصم", fSmall, Brushes.Black, new RectangleF(xDisc, y, cDisc, 15), sfCenter);
            g.DrawString("الجملة", fSmall, Brushes.Black, new RectangleF(xTot, y, cTot, 15), sfCenter);
            y += 16f;
            g.DrawLine(pen, x, y, right, y);

            foreach (var item in invoice.Items)
            {
                string itemName = item.ItemNameAr ?? string.Empty;
                // اسم الصنف الطويل بيتلف على أكثر من سطر
                float nh = Math.Max(15f, g.MeasureString(itemName, fBold, (int)(w - 6), sfCenter).Height + 3f);
                g.DrawString(itemName, fBold, Brushes.Black, new RectangleF(x + 3, y, w - 6, nh), sfCenter);
                y += nh;

                long gross = item.UnitPricePiastres * item.QuantitySold;
                int pct = ReceiptText.DiscountPercent(gross, item.DiscountPiastres);
                string qtyText = $"{item.QuantitySold.ToString(System.Globalization.CultureInfo.InvariantCulture)} {UnitBreakdownHelper.UnitLevelName(item.UnitLevel)}";

                g.DrawString(qtyText, fSmall, Brushes.Black, new RectangleF(xQty, y, cQty, 15), sfCenter);
                g.DrawString(ReceiptText.Amount(item.UnitPricePiastres), fSmall, Brushes.Black, new RectangleF(xPrice, y, cPrice, 15), sfNumCenter);
                g.DrawString(pct.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%", fSmall, Brushes.Black, new RectangleF(xDisc, y, cDisc, 15), sfNumCenter);
                g.DrawString(ReceiptText.Amount(item.TotalPiastres), fBold, Brushes.Black, new RectangleF(xTot, y, cTot, 15), sfNumCenter);
                y += 16f;
                g.DrawLine(pen, x, y, right, y);
            }
            g.DrawRectangle(pen, x, tableTop, w, y - tableTop);

            // ===== 3) صندوق المجاميع =====
            y += 6f;
            float totalsTop = y;
            float valW = w * 0.40f;

            void TotalRow(string label, long piastres, Font font, float rowH)
            {
                g.DrawString(label, font, Brushes.Black, new RectangleF(x + valW + 4, y, w - valW - 8, rowH), sfRight);
                g.DrawString(ReceiptText.Amount(piastres), font, Brushes.Black, new RectangleF(x, y, valW, rowH), sfNumCenter);
                y += rowH;
                g.DrawLine(pen, x, y, right, y);
            }

            TotalRow("القيمة الأولية", invoice.SubtotalPiastres, fSmall, 20f);
            if (invoice.DiscountPiastres > 0)
                TotalRow("إجمالي الخصم", invoice.DiscountPiastres, fSmall, 20f);
            if (invoice.VatPiastres > 0)
                TotalRow("ضريبة القيمة المضافة", invoice.VatPiastres, fSmall, 20f);
            TotalRow("صافي القيمة", invoice.TotalPiastres, fNet, 26f);
            TotalRow("المبلغ المدفوع", invoice.PaidPiastres, fBold, 22f);
            if (invoice.RemainingPiastres > 0)
                TotalRow("المتبقي", invoice.RemainingPiastres, fBold, 22f);

            // تفصيل الدفع المقسّم: سطر لكل طريقة بمبلغها
            if (invoice.IsSplitPayment)
            {
                foreach (var part in invoice.Payments)
                    TotalRow(InvoicePayment.MethodName(part.Method), part.AmountPiastres, fSmall, 18f);
            }

            g.DrawLine(pen, x + valW, totalsTop, x + valW, y);
            g.DrawRectangle(pen, x, totalsTop, w, y - totalsTop);
            y += 6f;

            // ===== 4) رمز الاستجابة السريع البسيط (إن فُعّل من الإعدادات) =====
            if (settings.EnableQrCode)
            {
                try
                {
                    string qrText = !string.IsNullOrEmpty(invoice.QrCodeText)
                        ? invoice.QrCodeText!
                        : EgyptQrHelper.BuildSimpleQrText(invoice.InvoiceNumber, invoice.Date, invoice.TotalPiastres, settings.TaxNumber);

                    using var qrGen = new QRCodeGenerator();
                    using var qrData = qrGen.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
                    using var qr = new QRCode(qrData);
                    using var qrBmp = qr.GetGraphic(3);

                    float qrSize = 90f;
                    float qrX = x + (w - qrSize) / 2f;
                    g.DrawImage(qrBmp, qrX, y, qrSize, qrSize);
                    y += qrSize + 6f;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "ThermalReceiptPrinter (طباعة رمز QR)");
                }
            }

            // ===== 5) التذييل =====
            g.DrawString("شكراً لزيارتكم - نتمنى لكم دوام الصحة والعافية", fSmall, Brushes.Black, new RectangleF(x, y, w, 16), sfCenter);
            y += 18f;
            g.DrawString(AppBranding.ProductByline, fTiny, Brushes.Gray, new RectangleF(x, y, w, 14), sfNumCenter);
            y += 14f;
            g.DrawString(AppBranding.Copyright, fTiny, Brushes.Gray, new RectangleF(x, y, w, 14), sfNumCenter);
            y += 14f;
            return y;
        }

        private static void DrawDottedLine(Graphics g, float y, float x1, float x2)
        {
            using var pen = new Pen(Color.Black, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawLine(pen, x1, y, x2, y);
        }

        private static void DrawDottedLine(Graphics g, float y, float width)
            => DrawDottedLine(g, y, 0, width);
    }
}

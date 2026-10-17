using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using ePrescriptions.Core.Helpers;

namespace ePrescriptions.App.Services
{
    /// <summary>
    /// يرسم ورقة استيكر صغيرة واحدة (سطر علوي: اسم الصيدلية يمين + التليفون يسار، الباركود، اسم الصنف)
    /// جوه مستطيلها بالظبط. كل رسمة بتتقص (Clip) على حدود الورقة فمفيش حاجة بتطلع بره ولا بتدخل في الورقة اللي جنبها.
    /// </summary>
    public static class MiniLabelRenderer
    {
        /// <summary>يحوّل الأرقام اللاتينية لهندية (٠١٢٣) زي الملصق المرجعي</summary>
        public static string ToArabicIndicDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                sb.Append(c >= '0' && c <= '9' ? (char)('\u0660' + (c - '0')) : c);
            return sb.ToString();
        }

        private static Font FitFont(Graphics g, string text, float startPt, float minPt, float maxWidth, FontStyle style)
        {
            float pt = startPt;
            while (pt > minPt)
            {
                var candidate = new Font("Tahoma", pt, style);
                if (g.MeasureString(text, candidate).Width <= maxWidth) return candidate;
                candidate.Dispose();
                pt -= 0.1f;
            }
            return new Font("Tahoma", minPt, style);
        }

        private static StringFormat MakeFormat(StringAlignment alignment, bool rtl)
        {
            var sf = new StringFormat
            {
                Alignment = alignment,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            };
            if (rtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            // الأرقام زي ما هي مكتوبة (لاتيني أو هندي حسب النص نفسه) بغض النظر عن لغة ويندوز
            sf.SetDigitSubstitution(1033, StringDigitSubstitute.None);
            return sf;
        }

        public static void DrawLabel(Graphics g, MiniLabelLayout l, int slot,
            string pharmacyName, string headerLeft, string barcode, string itemName, bool itemNameIsArabic)
        {
            float top = l.LabelTop(slot);
            var labelRect = new RectangleF(0, top, l.LabelWidth, l.LabelHeight);

            var oldClip = g.Clip;
            var oldHint = g.TextRenderingHint;
            var oldSmoothing = g.SmoothingMode;
            var oldPixelOffset = g.PixelOffsetMode;
            try
            {
                g.SetClip(labelRect);
                // طابعة حرارية 1-bit: نص مظبوط على الشبكة وحواف حادة للأعمدة
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.None;

                float side = MiniLabelLayout.SideMarginUnits;
                float innerW = l.LabelWidth - side * 2;

                using var brush = new SolidBrush(Color.Black);
                using var sfRight = MakeFormat(StringAlignment.Near, true);   // عربي: Near = اليمين
                using var sfLeft = MakeFormat(StringAlignment.Near, false);   // أرقام: Near = اليسار
                using var sfCenterAr = MakeFormat(StringAlignment.Center, true);
                using var sfCenterEn = MakeFormat(StringAlignment.Center, false);

                // 1) السطر العلوي
                bool hasName = !string.IsNullOrEmpty(pharmacyName);
                bool hasLeft = !string.IsNullOrEmpty(headerLeft);
                if (hasName || hasLeft)
                {
                    float rightW = hasLeft ? innerW * 0.62f : innerW;
                    float leftW = innerW - rightW;
                    float y = top + l.HeaderY;
                    if (hasName)
                    {
                        using var f = FitFont(g, pharmacyName, l.HeaderPt, 3.2f, rightW - 1f, FontStyle.Bold);
                        g.DrawString(pharmacyName, f, brush, new RectangleF(side + leftW, y, rightW, l.HeaderH), sfRight);
                    }
                    if (hasLeft)
                    {
                        using var f = FitFont(g, headerLeft, l.HeaderPt, 3.2f, leftW - 1f, FontStyle.Bold);
                        g.DrawString(headerLeft, f, brush, new RectangleF(side, y, leftW, l.HeaderH), sfLeft);
                    }
                }

                // 2) الباركود: أعمدة بتترسم مباشرة بعرض وحدة = أرقام صحيحة من نقاط الطابعة، ومتوسّط أفقياً
                DrawBars(g, l, top, barcode, brush, sfCenterEn);

                // 3) اسم الصنف تحت الباركود (الخط بيصغر لو طويل بدل ما يتقص)
                if (!string.IsNullOrEmpty(itemName))
                {
                    using var f = FitFont(g, itemName, l.NamePt, 3.2f, innerW - 1f, FontStyle.Bold);
                    g.DrawString(itemName, f, brush, new RectangleF(side, top + l.NameY, innerW, l.NameH),
                        itemNameIsArabic ? sfCenterAr : sfCenterEn);
                }
            }
            finally
            {
                g.Clip = oldClip;
                g.TextRenderingHint = oldHint;
                g.SmoothingMode = oldSmoothing;
                g.PixelOffsetMode = oldPixelOffset;
            }
        }

        private static void DrawBars(Graphics g, MiniLabelLayout l, float labelTop, string barcode, Brush brush, StringFormat fallbackFormat)
        {
            try
            {
                string pattern = Code128Encoder.Pattern(Code128Encoder.Encode(barcode));
                int totalModules = 0;
                foreach (char ch in pattern) totalModules += ch - '0';

                float dpi = g.DpiX > 0 ? g.DpiX : MiniLabelLayout.DefaultPrinterDpi;
                int dots = l.ModuleDots(totalModules, dpi);
                float moduleUnits = dots * 100f / dpi;
                float totalW = totalModules * moduleUnits;
                float x = (l.LabelWidth - totalW) / 2f;
                float y = labelTop + l.BarcodeY;
                float h = l.BarsHeightUnits;

                bool isBar = true;
                foreach (char ch in pattern)
                {
                    float w = (ch - '0') * moduleUnits;
                    if (isBar) g.FillRectangle(brush, x, y, w, h);
                    x += w;
                    isBar = !isBar;
                }
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"تعذر رسم الباركود، طُبع النص بدلاً منه: {ex.Message}", "MiniLabelRenderer");
                using var f = new Font("Tahoma", 4f, FontStyle.Bold);
                g.DrawString(barcode, f, brush,
                    new RectangleF(0, labelTop + l.BarcodeY, l.LabelWidth, l.BarsHeightUnits), fallbackFormat);
            }
        }
    }
}

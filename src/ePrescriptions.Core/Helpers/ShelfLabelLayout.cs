using System;

namespace ePrescriptions.Core.Helpers
{
    /// <summary>
    /// توزيع ملصق الرف على شكل الملصق المرجعي: سطر علوي (اسم الصيدلية + التليفون)، الباركود (أعمدة فقط)،
    /// ثم اسم الصنف تحت. كل الأبعاد بوحدة 1/100 بوصة زي PrintDocument، وارتفاع الأعمدة بالبكسل 96 DPI.
    /// الكتلة كلها بتتوسّط رأسياً في الملصق، وارتفاع الباركود بياخد المتاح (بحد أقصى) بدل ما يتقسم لنص.
    /// منطق بحت (من غير رسم) علشان يتختبر.
    /// </summary>
    public sealed class ShelfLabelLayout
    {
        public const int MinBarsPx = 14;
        public const int MaxBarsPx = 44;
        public const int QuietZonePx = 4;
        private const int Gap = 1;

        public int PageWidth { get; private set; }
        public int PageHeight { get; private set; }
        public int Margin { get; private set; }

        public float HeaderPt { get; private set; }
        public float NamePt { get; private set; }

        public int HeaderY { get; private set; }
        public int HeaderH { get; private set; }
        public int BarcodeY { get; private set; }
        public int NameY { get; private set; }
        public int NameH { get; private set; }

        /// <summary>ارتفاع صورة الباركود بالبكسل (96 DPI) — كلها أعمدة، من غير أرقام ولا حواف فاضية</summary>
        public int BarsHeightPx { get; private set; }

        /// <summary>ارتفاع صورة الباركود بوحدات الصفحة</summary>
        public int BarcodeHeightUnits => LabelLayout.PixelsToUnits(BarsHeightPx);

        /// <summary>false = الملصق أقصر من إن الكتلة تتحط فيه بحد أدنى مقروء للباركود</summary>
        public bool Fits { get; private set; }

        public static ShelfLabelLayout Compute(double widthMm, double heightMm)
        {
            int w = Math.Max(120, LabelLayout.MmToHundredths(widthMm));
            int h = Math.Max(45, LabelLayout.MmToHundredths(heightMm));

            bool small = h < 60;
            int margin = small ? 2 : 3;
            float headerPt = small ? 5.5f : 6.5f;
            float namePt = small ? 6f : 7.5f;
            int headerH = (int)Math.Ceiling(headerPt * 100f / 72f * 1.25f);
            int nameH = (int)Math.Ceiling(namePt * 100f / 72f * 1.25f);

            int availUnits = h - margin * 2 - headerH - nameH - Gap * 2;
            int availPx = LabelLayout.UnitsToPixels(Math.Max(0, availUnits));
            int bars = Math.Min(MaxBarsPx, Math.Max(MinBarsPx, availPx));

            int barcodeUnits = LabelLayout.PixelsToUnits(bars);
            int stackH = headerH + Gap + barcodeUnits + Gap + nameH;
            int top = Math.Max(margin, (h - stackH) / 2);

            var l = new ShelfLabelLayout
            {
                PageWidth = w,
                PageHeight = h,
                Margin = margin,
                HeaderPt = headerPt,
                NamePt = namePt,
                HeaderH = headerH,
                NameH = nameH,
                BarsHeightPx = bars
            };
            l.HeaderY = top;
            l.BarcodeY = l.HeaderY + headerH + Gap;
            l.NameY = l.BarcodeY + barcodeUnits + Gap;
            l.Fits = availPx >= MinBarsPx && l.NameY + nameH <= h - margin + 1;
            return l;
        }

        /// <summary>عرض الباركود (Code128 بوحدة 1 بكسل) داخل عرض الملصق بعد الهوامش؟</summary>
        public bool BarcodeFitsWidth(int totalModules)
            => LabelLayout.BarcodeWidthUnits(totalModules, 1, QuietZonePx) <= PageWidth - Margin * 2;
    }
}

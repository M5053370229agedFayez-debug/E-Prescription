using System;

namespace ePrescriptions.Core.Helpers
{
    /// <summary>
    /// توزيع الاستيكر الصغير جداً (مثلاً 30 × 7 مم) اللي بيجي في اللفة كل ورقتين لاصقين في بعض
    /// وبين كل زوج والتاني فراغ حوالي 1 مم. الصفحة اللي بتشوفها الطابعة = ورقتين فوق بعض + الفراغ،
    /// وكل ورقة ليها مستطيلها وبيتحط فيه (سطر علوي + باركود + اسم الصنف) متوسّط رأسياً وأفقياً
    /// ومقصوص (Clip) على حدود الورقة فمفيش حاجة تطلع بره.
    /// كل الأبعاد بوحدة 1/100 بوصة زي PrintDocument. منطق بحت (من غير رسم) علشان يتختبر.
    /// </summary>
    public sealed class MiniLabelLayout
    {
        /// <summary>أي استيكر ارتفاعه أقل من كده بيتعامل كاستيكر صغير مزدوج</summary>
        public const double MiniHeightThresholdMm = 12.0;
        public const int DefaultStackCount = 2;
        public const double DefaultGapMm = 1.0;

        public const int MinBarsPx = 8;
        public const int SideMarginUnits = 4;       // ≈ 1 مم من كل جنب
        public const int MinQuietUnits = 6;         // ≈ 1.5 مم هدوء حوالين الباركود
        public const float DefaultPrinterDpi = 203f;

        public int LabelWidth { get; private set; }
        public int LabelHeight { get; private set; }
        public int StackCount { get; private set; }
        public int GapUnits { get; private set; }
        public int PageWidth => LabelWidth;
        public int PageHeight => StackCount * LabelHeight + GapUnits;

        public int Margin { get; private set; }
        public float HeaderPt { get; private set; }
        public float NamePt { get; private set; }
        public int HeaderH { get; private set; }
        public int NameH { get; private set; }

        // المواضع بالنسبة لأعلى الورقة الواحدة
        public int HeaderY { get; private set; }
        public int BarcodeY { get; private set; }
        public int NameY { get; private set; }
        public int BarsHeightUnits { get; private set; }

        /// <summary>false = الورقة قصيرة جداً حتى للحد الأدنى المقروء من الباركود</summary>
        public bool Fits { get; private set; }

        public static bool IsMini(double heightMm) => heightMm > 0 && heightMm < MiniHeightThresholdMm;

        public int LabelTop(int slot) => slot * LabelHeight;

        public static MiniLabelLayout Compute(double widthMm, double heightMm,
            int stackCount = DefaultStackCount, double gapMm = DefaultGapMm)
        {
            int w = Math.Max(60, LabelLayout.MmToHundredths(widthMm));
            int h = Math.Max(20, LabelLayout.MmToHundredths(heightMm));
            int stack = Math.Max(1, stackCount);
            int gap = stack > 1 ? Math.Max(0, LabelLayout.MmToHundredths(gapMm)) : 0;

            const int margin = 1;
            const float headerPt = 4f;
            const float namePt = 4f;
            int headerH = (int)Math.Ceiling(headerPt * 100f / 72f * 1.05f);
            int nameH = (int)Math.Ceiling(namePt * 100f / 72f * 1.05f);

            int bars = h - margin * 2 - headerH - nameH;
            bool fits = LabelLayout.UnitsToPixels(Math.Max(0, bars)) >= MinBarsPx;
            bars = Math.Max(LabelLayout.PixelsToUnits(MinBarsPx), bars);

            int stackH = headerH + bars + nameH;
            int top = Math.Max(0, (h - stackH) / 2);

            var l = new MiniLabelLayout
            {
                LabelWidth = w,
                LabelHeight = h,
                StackCount = stack,
                GapUnits = gap,
                Margin = margin,
                HeaderPt = headerPt,
                NamePt = namePt,
                HeaderH = headerH,
                NameH = nameH,
                BarsHeightUnits = bars
            };
            l.HeaderY = top;
            l.BarcodeY = l.HeaderY + headerH;
            l.NameY = l.BarcodeY + bars;
            l.Fits = fits && l.NameY + nameH <= h;
            return l;
        }

        /// <summary>
        /// عرض الوحدة (module) بالنقاط الفعلية للطابعة: رقم صحيح عشان الأعمدة تطلع حادة ومتناسقة،
        /// أكبر رقم يخلّي الباركود يتسع في عرض الورقة بعد منطقة الهدوء. 1 كحد أدنى.
        /// </summary>
        public int ModuleDots(int totalModules, float printerDpi)
        {
            if (printerDpi <= 0) printerDpi = DefaultPrinterDpi;
            double unitsPerDot = 100.0 / printerDpi;
            double avail = LabelWidth - 2 * MinQuietUnits;
            int dots = (int)Math.Floor(avail / (Math.Max(1, totalModules) * unitsPerDot));
            return Math.Max(1, dots);
        }

        /// <summary>عرض الباركود المطبوع بوحدات الصفحة</summary>
        public double BarcodeWidthUnits(int totalModules, float printerDpi)
        {
            if (printerDpi <= 0) printerDpi = DefaultPrinterDpi;
            return totalModules * ModuleDots(totalModules, printerDpi) * (100.0 / printerDpi);
        }

        /// <summary>عدد الصفحات (كل صفحة = StackCount ورقة) لعدد ملصقات معين</summary>
        public int PagesFor(int labelCount) => (Math.Max(1, labelCount) + StackCount - 1) / StackCount;
    }
}

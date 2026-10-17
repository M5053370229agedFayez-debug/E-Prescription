using ePrescriptions.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace ePrescriptions.Tests
{
    /// <summary>
    /// استيكر 30×7 مم (ورقتين لاصقين فوق بعض + فراغ 1 مم): كل عنصر لازم يفضل جوه ورقته ومتوسّط.
    /// الرسم الفعلي (GDI+) محتاج تجربة على الطابعة الحقيقية.
    /// </summary>
    public class MiniLabelLayoutTests
    {
        [Theory]
        [InlineData(6.9, true)]
        [InlineData(7, true)]
        [InlineData(11.9, true)]
        [InlineData(12, false)]
        [InlineData(25, false)]
        public void IsMini_ShouldOnlyApplyToShortStickers(double heightMm, bool expected)
        {
            MiniLabelLayout.IsMini(heightMm).Should().Be(expected);
        }

        [Fact]
        public void Page_ShouldBeTwoLabelsPlusOneMmGap()
        {
            var l = MiniLabelLayout.Compute(30, 7);
            l.StackCount.Should().Be(2);
            l.LabelWidth.Should().Be(LabelLayout.MmToHundredths(30));
            l.LabelHeight.Should().Be(LabelLayout.MmToHundredths(7));
            l.GapUnits.Should().Be(LabelLayout.MmToHundredths(1));
            l.PageHeight.Should().Be(2 * l.LabelHeight + l.GapUnits);
            l.LabelTop(1).Should().Be(l.LabelHeight);
        }

        [Theory]
        [InlineData(30, 7)]
        [InlineData(30, 8)]
        [InlineData(38, 10)]
        public void EveryElement_ShouldStayInsideItsOwnLabel_InOrder(double w, double h)
        {
            var l = MiniLabelLayout.Compute(w, h);

            l.Fits.Should().BeTrue();
            l.HeaderY.Should().BeGreaterThanOrEqualTo(0);
            l.BarcodeY.Should().BeGreaterThanOrEqualTo(l.HeaderY + l.HeaderH);
            l.NameY.Should().BeGreaterThanOrEqualTo(l.BarcodeY + l.BarsHeightUnits);
            (l.NameY + l.NameH).Should().BeLessThanOrEqualTo(l.LabelHeight);
        }

        [Fact]
        public void Block_ShouldBeVerticallyCentered_WithinOneUnit()
        {
            var l = MiniLabelLayout.Compute(30, 7);
            int topSpace = l.HeaderY;
            int bottomSpace = l.LabelHeight - (l.NameY + l.NameH);
            System.Math.Abs(topSpace - bottomSpace).Should().BeLessThanOrEqualTo(1);
        }

        [Theory]
        [InlineData(203f)]
        [InlineData(300f)]
        public void Barcode_ShouldFitInsideLabelWidth_WithQuietZone(float dpi)
        {
            var l = MiniLabelLayout.Compute(30, 7);
            foreach (var code in new[] { "6221025008848", "9900000123" })
            {
                int modules = Code128Encoder.TotalModules(code);
                l.ModuleDots(modules, dpi).Should().BeGreaterThanOrEqualTo(1);
                l.BarcodeWidthUnits(modules, dpi).Should()
                    .BeLessThanOrEqualTo(l.LabelWidth - 2 * MiniLabelLayout.MinQuietUnits + 0.01);
            }
        }

        [Fact]
        public void ShorterBatchCode_ShouldGetWiderModules_ThanEan13_At203Dpi()
        {
            var l = MiniLabelLayout.Compute(30, 7);
            l.ModuleDots(Code128Encoder.TotalModules("9900000123"), 203f)
                .Should().BeGreaterThanOrEqualTo(l.ModuleDots(Code128Encoder.TotalModules("6221025008848"), 203f));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 2)]
        [InlineData(7, 4)]
        public void PagesFor_ShouldPackTwoLabelsPerPage(int labels, int pages)
        {
            MiniLabelLayout.Compute(30, 7).PagesFor(labels).Should().Be(pages);
        }
    }
}

using System;
using ePrescriptions.Core.Helpers;
using ePrescriptions.Domain.Helpers;
using FluentAssertions;
using Xunit;

namespace ePrescriptions.Tests
{
    /// <summary>
    /// منطق شكل الإيصال والملصق الجديد (زي الإيصال والملصق المرجعيين): فصل كلمة "صيدلية"، نسبة الخصم، تنسيق المبالغ،
    /// وتوزيع ملصق الرف جوه مقاسه. الرسم نفسه (GDI+) لسه محتاج تجربة على الطابعة الحقيقية.
    /// </summary>
    public class ReceiptAndShelfLabelTests
    {
        [Fact]
        public void SplitPharmacyName_ShouldSeparatePharmacyWord()
        {
            ReceiptText.SplitPharmacyName("صيدلية دكتورة ندى", out string prefix, out string name);
            prefix.Should().Be("صيدلية");
            name.Should().Be("دكتورة ندى");
        }

        [Theory]
        [InlineData("Nada Pharmacy", "Nada Pharmacy")]
        [InlineData("صيدلية", "صيدلية")]
        [InlineData("  دكتور أحمد  ", "دكتور أحمد")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void SplitPharmacyName_WithoutNameAfterWord_ShouldKeepFullNameAndNoPrefix(string? full, string expectedName)
        {
            ReceiptText.SplitPharmacyName(full, out string prefix, out string name);
            prefix.Should().BeEmpty();
            name.Should().Be(expectedName);
        }

        [Theory]
        [InlineData(7600L, 0L, 0)]
        [InlineData(7600L, 760L, 10)]
        [InlineData(10000L, 1250L, 13)]   // 12.5% بتتقرّب لـ 13
        [InlineData(0L, 500L, 0)]
        [InlineData(5000L, -5L, 0)]
        public void DiscountPercent_ShouldRoundToWholePercent(long gross, long discount, int expected)
        {
            ReceiptText.DiscountPercent(gross, discount).Should().Be(expected);
        }

        [Theory]
        [InlineData(7600L, "76")]
        [InlineData(10300L, "103")]
        [InlineData(550L, "5.50")]
        [InlineData(5L, "0.05")]
        [InlineData(0L, "0")]
        public void Amount_ShouldUseLatinDigits_AndHideZeroFraction(long piastres, string expected)
        {
            ReceiptText.Amount(piastres).Should().Be(expected);
        }

        [Theory]
        [InlineData(38.0, 12.0)]
        [InlineData(38.0, 25.0)]
        [InlineData(50.0, 25.0)]
        [InlineData(40.0, 30.0)]
        [InlineData(58.0, 40.0)]
        public void ShelfLabelLayout_ShouldKeepEverythingInsideLabel_InThisOrder(double widthMm, double heightMm)
        {
            var l = ShelfLabelLayout.Compute(widthMm, heightMm);

            l.Fits.Should().BeTrue();
            l.PageWidth.Should().Be(LabelLayout.MmToHundredths(widthMm));
            l.PageHeight.Should().Be(LabelLayout.MmToHundredths(heightMm));

            l.HeaderY.Should().BeGreaterThanOrEqualTo(l.Margin);
            l.BarcodeY.Should().BeGreaterThanOrEqualTo(l.HeaderY + l.HeaderH);
            l.NameY.Should().BeGreaterThanOrEqualTo(l.BarcodeY + l.BarcodeHeightUnits);
            (l.NameY + l.NameH).Should().BeLessThanOrEqualTo(l.PageHeight - l.Margin + 1);

            l.BarsHeightPx.Should().BeGreaterThanOrEqualTo(ShelfLabelLayout.MinBarsPx);
            l.BarsHeightPx.Should().BeLessThanOrEqualTo(ShelfLabelLayout.MaxBarsPx);
        }

        [Fact]
        public void ShelfLabelLayout_TallerLabel_ShouldGetTallerBarcode_UpToCap()
        {
            var small = ShelfLabelLayout.Compute(38, 12);
            var big = ShelfLabelLayout.Compute(50, 25);
            big.BarsHeightPx.Should().BeGreaterThan(small.BarsHeightPx);
            big.BarsHeightPx.Should().Be(ShelfLabelLayout.MaxBarsPx);
        }

        [Fact]
        public void ShelfLabelLayout_Ean13Barcode_ShouldFitOn50mmLabel()
        {
            int modules = Code128Encoder.TotalModules("6221025008848");
            ShelfLabelLayout.Compute(50, 25).BarcodeFitsWidth(modules).Should().BeTrue();
        }

        [Fact]
        public void ShelfLabelLayout_TooShortLabel_ShouldClampToMinimumPageHeight()
        {
            // 8 مم بتتجبر لأدنى ارتفاع صفحة (45 وحدة)
            ShelfLabelLayout.Compute(38, 8).PageHeight.Should().Be(45);
        }
    }
}

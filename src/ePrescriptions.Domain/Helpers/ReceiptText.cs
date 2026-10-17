using System;
using System.Globalization;
using ePrescriptions.Domain.Enums;

namespace ePrescriptions.Domain.Helpers
{
    /// <summary>نصوص ثابتة في الإيصال الحراري (منطق بحت علشان يتختبر)</summary>
    public static class ReceiptText
    {
        /// <summary>
        /// عنوان الإيصال حسب النوع وطريقة الدفع. كان بيطبع "فاتورة مبيعات نقدية" لأي فاتورة بيع حتى لو آجل أو تأمين.
        /// </summary>
        public static string InvoiceTitle(InvoiceType type, PaymentMethod method)
        {
            if (type == InvoiceType.Return) return "إشعار دائن (مرتجع مبيعات)";
            switch (method)
            {
                case PaymentMethod.Cash: return "فاتورة مبيعات نقدية";
                case PaymentMethod.Network: return "فاتورة مبيعات (شبكة / بطاقة)";
                case PaymentMethod.OnAccount: return "فاتورة مبيعات آجلة";
                case PaymentMethod.Insurance: return "فاتورة مبيعات تأمين";
                default: return "فاتورة مبيعات";
            }
        }

        /// <summary>
        /// يفصل كلمة "صيدلية" عن باقي الاسم علشان الإيصال يكتبها سطر صغير فوق والاسم بخط كبير تحته.
        /// لو الاسم مش بادئ بـ "صيدلية" (أو ما فيهوش غيرها) بيرجع prefix فاضي والاسم كامل.
        /// </summary>
        public static void SplitPharmacyName(string? full, out string prefix, out string name)
        {
            const string word = "صيدلية";
            string trimmed = (full ?? string.Empty).Trim();
            if (trimmed.StartsWith(word, StringComparison.Ordinal) && trimmed.Length > word.Length)
            {
                prefix = word;
                name = trimmed.Substring(word.Length).Trim();
                if (name.Length > 0) return;
            }
            prefix = string.Empty;
            name = trimmed;
        }

        /// <summary>نسبة الخصم المئوية للبند (من إجمالي البند قبل الخصم)، مقرّبة لأقرب رقم صحيح</summary>
        public static int DiscountPercent(long grossPiastres, long discountPiastres)
        {
            if (grossPiastres <= 0 || discountPiastres <= 0) return 0;
            double pct = discountPiastres * 100.0 / grossPiastres;
            return (int)Math.Round(pct, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// مبلغ للطباعة بأرقام لاتينية: بدون كسور لو المبلغ جنيهات كاملة (76)، وإلا بخانتين (5.50).
        /// </summary>
        public static string Amount(long piastres)
        {
            if (piastres % 100 == 0)
                return (piastres / 100).ToString(CultureInfo.InvariantCulture);
            return (piastres / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}

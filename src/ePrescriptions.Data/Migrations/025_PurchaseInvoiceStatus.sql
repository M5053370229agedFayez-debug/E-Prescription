-- Migration 025: إضافة عمود Status وتفاصيل الإلغاء لجدول PurchaseInvoices
ALTER TABLE PurchaseInvoices ADD COLUMN Status INTEGER NOT NULL DEFAULT 1;
ALTER TABLE PurchaseInvoices ADD COLUMN CancelledAt TEXT NULL;
ALTER TABLE PurchaseInvoices ADD COLUMN CancelReason TEXT NULL;

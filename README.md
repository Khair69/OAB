# OAB — Customizable Ledger for Souk Shops | دفتر حسابات قابل للتخصيص لمحلات السوق

**English** · [العربية ↓](#العربية)

---

## English

Offline-first money tracking for small resellers who currently use a notebook
and pen: what the shop owes suppliers, what customers owe the shop, and whether
things are paid. Deliberately *less* than a POS — small shops avoid POS systems
because they're complicated and full of unused features, so each shop gets a
build with **only** the features they asked for.

### Stack

.NET 10 · .NET MAUI (Android first, Windows for desk testing) · SQLite + EF Core
· CommunityToolkit.Mvvm · xUnit. Arabic RTL + English with live language
switching, per-shop wording, optional Arabic-Indic digits.

### Layout

```
src/Oab.Core          Money engine. Pure C#: Party, LedgerEntry, LedgerService. No UI/DB deps.
src/Oab.Data          EF Core + SQLite: OabDbContext, LedgerStore, migrations.
src/Oab.App           Shared MAUI shell: module host (IOabModule), OabShell flyout,
                      localization (Strings.resx + ar), ShopConfig, money formatting.
src/Oab.Modules/*     Optional features. Each is self-contained: pages, VMs, DI registration.
customers/*           One tiny project per shop: ShopConfig + list of modules. ~40 lines.
tests/                xUnit suites for the money engine and the SQLite store.
```

### The two rules that keep this maintainable

1. **The ledger is append-only.** Every money movement is an immutable signed
   `LedgerEntry` (positive = the party owes the shop, negative = the shop owes
   the party). Balances are always `SUM(entries)`; corrections are new
   `Adjustment` entries with a mandatory note. Never add a mutable balance
   column. This is also what will make multi-device sync cheap later.

2. **Custom work never touches core.** A shop wanting a special feature gets a
   new `IOabModule` in *their* customer folder. If a second shop wants it,
   promote it to `src/Oab.Modules/`. Never fork the repo per customer.

### Onboarding a new shop

1. Copy `customers/Oab.Customer.Template` to `customers/Oab.Customer.<Shop>`;
   rename the csproj/namespace and `ApplicationId`.
2. Edit `MauiProgram.cs`: set `ShopConfig` (name, currency symbol, default
   culture, Arabic-Indic digits, label overrides) and pass exactly the modules
   they want to `UseOab(...)`.
3. Build the APK: `dotnet publish -f net10.0-android -c Release`.

Per-shop wording without code: `LabelOverrides = { ["ar:Purchases_Title"] = "الدفتر" }`.

### Developing

```powershell
dotnet test                                      # money engine + SQLite store suites
dotnet build customers/Oab.Customer.Template -f net10.0-windows10.0.19041.0   # run on the dev PC
dotnet build customers/Oab.Customer.Template -f net10.0-android               # APK
```

Schema changes: edit entities in `Oab.Core`, mapping in `Oab.Data/OabDbContext`,
then `dotnet ef migrations add <Name> --project src/Oab.Data`. Apps run
`Database.Migrate()` at startup, so upgrades are automatic.

### Not in v1 (on purpose)

Multi-device sync, cloud, receipt printing, barcode scanning, advanced reports.
Backup for now = exporting the SQLite file (planned as a small module).

---

<div dir="rtl" align="right">

## العربية

تطبيق لتتبّع الحسابات يعمل دون إنترنت، موجّه للمحلات الصغيرة التي ما زالت
تستعمل الدفتر والقلم: ماذا يدين المحل للموردين، وماذا يدين الزبائن للمحل، وهل
دُفع المبلغ أم لا. التطبيق مقصود أن يكون **أبسط** من أنظمة نقاط البيع — فأصحاب
المحلات يتجنبونها لأنها معقّدة ومليئة بميزات لا يستعملونها، لذلك كل محل يحصل
على نسخة فيها **فقط** الميزات التي طلبها.

### التقنيات

‎.NET 10 · ‎.NET MAUI (أندرويد أولًا، وويندوز للتجربة على الحاسوب) ·
SQLite + EF Core · CommunityToolkit.Mvvm · xUnit. واجهة عربية (من اليمين إلى
اليسار) وإنجليزية مع تبديل فوري للّغة، وصياغة خاصة بكل محل، وأرقام
عربية-هندية اختيارية (٠١٢٣).

### بنية المشروع

<div dir="ltr" align="left">

```
src/Oab.Core          محرك الحسابات: أطراف، قيود دفتر، خدمة القيود — C#‎ صِرف بلا واجهة أو قاعدة بيانات
src/Oab.Data          طبقة البيانات: EF Core + SQLite مع الترحيلات (migrations)
src/Oab.App           الهيكل المشترك للتطبيق: نظام الوحدات، القائمة الجانبية، الترجمة، إعدادات المحل
src/Oab.Modules/*     الميزات الاختيارية — كل ميزة وحدة مستقلة بصفحاتها وخدماتها
customers/*           مشروع صغير لكل محل: إعدادات المحل + قائمة الوحدات المطلوبة (~40 سطرًا)
tests/                اختبارات محرك الحسابات وطبقة البيانات
```

</div>

### القاعدتان اللتان تحفظان المشروع من الفوضى

١. **دفتر القيود لا يُعدَّل، بل يُضاف إليه فقط.** كل حركة مالية قيدٌ ثابت بإشارة:
موجب = الطرف مدين للمحل، سالب = المحل مدين للطرف. الرصيد دائمًا هو مجموع
القيود، وتصحيح الخطأ يكون بقيد «تسوية» جديد مع ملاحظة إلزامية — تمامًا كما
يعمل دفتر الورق. لا تُضِف أبدًا عمود رصيد قابل للتعديل. هذا التصميم هو ما
سيجعل المزامنة بين الأجهزة سهلة مستقبلًا.

٢. **التخصيص لا يلمس النواة أبدًا.** إذا طلب محلٌ ميزة خاصة، تُكتب كوحدة
`IOabModule` جديدة داخل مجلد ذلك المحل. وإذا طلبها محل ثانٍ، تُنقل إلى
الوحدات المشتركة. لا تنسخ المستودع لكل زبون أبدًا.

### إضافة محل جديد

١. انسخ ‎`customers/Oab.Customer.Template`‎ إلى ‎`customers/Oab.Customer.<اسم المحل>`‎ وغيّر اسم المشروع ومعرّف التطبيق.

٢. عدّل ‎`MauiProgram.cs`‎: اضبط إعدادات المحل (الاسم، رمز العملة، اللغة،
الأرقام، تخصيص الكلمات) ومرّر الوحدات المطلوبة فقط إلى ‎`UseOab(...)`‎.

٣. ابنِ ملف APK: ‏‎`dotnet publish -f net10.0-android -c Release`‎

تخصيص الكلمات دون برمجة: ‎`LabelOverrides = { ["ar:Purchases_Title"] = "الدفتر" }`‎

### ليس في النسخة الأولى (عن قصد)

المزامنة بين الأجهزة، السحابة، طباعة الفواتير، قارئ الباركود، التقارير
المتقدمة. النسخ الاحتياطي حاليًا = تصدير ملف قاعدة البيانات (مخطط له كوحدة
صغيرة).

</div>

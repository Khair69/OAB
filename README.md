# OAB — Customizable Ledger for Souk Shops | دفتر حسابات قابل للتخصيص لمحلات السوق

**English** · [العربية ↓](#العربية)

---

## English

Offline-first money tracking for small resellers who currently use a notebook
and pen: what the shop owes suppliers, what customers owe the shop, and whether
things are paid. Deliberately *less* than a POS — small shops avoid POS systems
because they're complicated and full of unused features, so each shop gets a
build with **only** the features they asked for.

### Documentation

Full technical documentation lives in **[`docs/`](docs/README.md)** —
architecture, the money engine, the data layer, every screen, localization,
per-shop customization, build/release, the design decisions and their reasoning,
and an honest status inventory. Start at the
[documentation index](docs/README.md).

### Stack

.NET 10 · .NET MAUI (Android first, Windows for desk testing) · SQLite + EF Core
· CommunityToolkit.Mvvm · xUnit. Arabic RTL + English with live language
switching, per-shop wording, optional Arabic-Indic digits — rendered *and* read
back.

### Layout

```
src/Oab.Core          Money engine. Pure C#: Party, LedgerEntry, LedgerService,
                      MoneyFormat/MoneyInput. No UI/DB deps.
src/Oab.Data          EF Core + SQLite: OabDbContext, LedgerStore, migrations.
src/Oab.App           Shared MAUI shell: module host (IOabModule), OabShell flyout,
                      localization (Strings.resx + ar), ShopConfig, money formatting,
                      Diagnostics/ (global exception handler + shareable errors.log).
src/Oab.Modules/*     Optional features. Each is self-contained: pages, VMs, DI registration.
                      Backup is effectively mandatory — see below.
customers/*           One tiny project per shop: ShopConfig + list of modules. ~40 lines.
tests/                xUnit suites for the money engine and the SQLite store.
```

### The two rules that keep this maintainable

1. **The ledger is append-only.** Every money movement is an immutable signed
   `LedgerEntry` (positive = the party owes the shop, negative = the shop owes
   the party). Balances are always `SUM(entries)`; corrections are new
   `Adjustment` entries with a mandatory note — tap an entry on a party's
   statement and say what it should have been. Never add a mutable balance
   column. This is also what will make multi-device sync cheap later.

2. **Custom work never touches core.** A shop wanting a special feature gets a
   new `IOabModule` in *their* customer folder. If a second shop wants it,
   promote it to `src/Oab.Modules/`. Never fork the repo per customer.

3. **Always ship `BackupModule`.** The book lives on one phone with no sync, so
   without backup a lost phone loses everything — worse than the paper notebook
   we replace. It offers a restorable `.db` snapshot (taken with `VACUUM INTO`,
   safe while the app is running) and a plain-text summary a person can read
   even without the app. Restore validates the file, keeps a `.pre-restore`
   copy, and migrates an older backup up to the current schema.

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
# Fast, cross-platform: money engine + SQLite store (this is what CI runs)
dotnet test tests/Oab.Core.Tests
dotnet test tests/Oab.Data.Tests
# View-model / presentation tests — MAUI-targeted, Windows only
dotnet test tests/Oab.App.Tests

dotnet build customers/Oab.Customer.Template -f net10.0-windows10.0.19041.0   # run on the dev PC
dotnet build customers/Oab.Customer.Template -f net10.0-android               # APK
```

Test layout: `Oab.Core.Tests` (ledger math, money in and out), `Oab.Data.Tests`
(real SQLite + migrations), `Oab.App.Tests` (view models — balance→text/colour,
role filtering, pay-remaining — plus the error log; runs headlessly on Windows).
`Oab.TestSupport` holds the shared in-memory store. Only the first two run in
CI, since the MAUI tests need the Windows MAUI workload.

**Every new `ILedgerStore` method needs a test in `Oab.Data.Tests`, not only in
`Oab.App.Tests`.** The in-memory store runs LINQ-to-Objects and will pass queries
the SQLite provider refuses to translate — that gap hid a `NotSupportedException`
on two shipped screens until the global exception handler surfaced it.

**Parse typed amounts with `MoneyInput.TryParseAmount`, never `decimal.TryParse`.**
The app renders Arabic-Indic digits, and .NET's `ar` uses `٫` and `٬` as its
separators, so a raw parse cannot read the app's own output. One parser, in Core,
where the tests are cheap.

After any manual run, read the crash log. It is at
`FileSystem.AppDataDirectory/errors.log`, and on a device the backup screen has a
**Send error report** card that shares it.

Schema changes: edit entities in `Oab.Core`, mapping in `Oab.Data/OabDbContext`,
then `dotnet ef migrations add <Name> --project src/Oab.Data`. Apps run
`Database.Migrate()` at startup, so upgrades are automatic.

### Repo conventions

- **`Directory.Build.props`** — shared build settings (nullable, implicit
  usings, analyzers) and the single list of package versions as `$(...)`
  variables. Bump a dependency there, not in each csproj.
- **No Central Package Management.** CPM breaks the MAUI SDK's implicit global
  usings in app-head projects, and this repo creates one head per customer, so
  versions are shared via the props variables above instead.
- **`global.json`** pins the .NET SDK feature band for reproducible builds.
- **`.editorconfig`** drives code style; `dotnet format` keeps it consistent.
- **CI** (`.github/workflows/ci.yml`) builds and tests `Oab.Core` + `Oab.Data`
  on every push/PR — the money engine, the part that must never break.
- **Nothing fails silently.** Every `async void` handler goes through
  `this.RunSafelyAsync(...)` (`Oab.App.Diagnostics`); anything that escapes is
  caught process-wide and written to a shareable `errors.log`. There is no
  console on a shopkeeper's phone, so an unlogged crash is a bug that can never
  be fixed.

### Not in v1 (on purpose)

Multi-device sync, cloud, receipt printing, barcode scanning, stock quantities,
advanced reports, iOS. Backup is a `.db` snapshot plus a readable text summary,
both sent through the Android share sheet — deliberately not cloud sync.

---

<div dir="rtl" align="right">

## العربية

تطبيق لتتبّع الحسابات يعمل دون إنترنت، موجّه للمحلات الصغيرة التي ما زالت
تستعمل الدفتر والقلم: ماذا يدين المحل للموردين، وماذا يدين الزبائن للمحل، وهل
دُفع المبلغ أم لا. التطبيق مقصود أن يكون **أبسط** من أنظمة نقاط البيع — فأصحاب
المحلات يتجنبونها لأنها معقّدة ومليئة بميزات لا يستعملونها، لذلك كل محل يحصل
على نسخة فيها **فقط** الميزات التي طلبها.

### التوثيق

التوثيق التقني الكامل موجود في مجلد ‎[`docs/`](docs/README.md)‎ (بالإنجليزية):
البنية، ومحرك الحسابات، وطبقة البيانات، وكل شاشة، والترجمة، والتخصيص لكل محل،
والبناء والإصدار، والقرارات التصميمية وأسبابها، وجرد صريح لما أُنجز وما تبقّى.

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

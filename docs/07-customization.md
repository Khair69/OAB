# 07 — Per-Shop Customization

[← 06 Localization & RTL](06-localization.md) · [Index](README.md) · Next: [08 — Build, Test & Release](08-build-test-release.md)

---

## 1. The thesis

"Custom build per shop" must **not** mean "fork the repository per shop" — that
drowns you after about five customers. Instead: a product line. One repository,
one shared core, and each customer is a tiny composition project.

A whole customer build is one file:

```csharp
// customers/Oab.Customer.Template/MauiProgram.cs
builder.UseOab(
    new ShopConfig
    {
        ShopName = "متجر تجريبي",
        CurrencySymbol = "د.ج",
        DefaultCulture = "ar",
        SupportedCultures = ["ar", "en"],
    },
    new PurchasesModule(),
    new SupplierDebtsModule(),
    new CustomerDebtsModule(),
    new BackupModule());
```

Features come from **which modules are listed**. Wording, currency, language and
digits come from **`ShopConfig`**. Neither requires touching `src/`.

A shop that "only wants to log what they buy and whether it's paid" gets
`PurchasesModule` + `SupplierDebtsModule` + `BackupModule`. Nothing else appears
in their UI — no hidden menu, no disabled button, no "coming soon".

## 2. Onboarding a new shop

### Step 1 — copy the template

```bash
cp -r customers/Oab.Customer.Template customers/Oab.Customer.Sami
```

Then, inside the new folder:

- rename `Oab.Customer.Template.csproj` → `Oab.Customer.Sami.csproj`;
- update `<RootNamespace>` and the `namespace` in `MauiProgram.cs`,
  `Platforms/Android/MainActivity.cs`, `Platforms/Android/MainApplication.cs`;
- set `<ApplicationTitle>` (what shows under the icon);
- set `<ApplicationId>` — **must be unique per shop**, e.g.
  `com.yourcompany.oab.sami`. Two shops sharing an id cannot be installed side
  by side and will collide on upgrade. *(The template still ships
  `com.companyname.oab.customer.template` — change it.)*
- set `<ApplicationDisplayVersion>` and `<ApplicationVersion>`;
- fix the relative `..\..\src\...` project references if you moved the folder
  depth (you normally won't).

Also delete `Oab.Customer.Template.csproj.user` if it was copied — it is a local
IDE file.

### Step 2 — add it to the solution

```bash
dotnet sln OAB.slnx add customers/Oab.Customer.Sami/Oab.Customer.Sami.csproj
```

### Step 3 — configure the shop

Edit `MauiProgram.cs`: fill in `ShopConfig` (§3) and pass exactly the modules
they asked for (§4). Add or remove the matching `<ProjectReference>` entries in
the csproj — a module that is referenced but not passed to `UseOab` is dead
weight in the APK; a module passed but not referenced will not compile.

### Step 4 — brand it

Replace `Resources/AppIcon/appicon.svg` and `appiconfg.svg`, and
`Resources/Splash/splash.svg`. Adjust the `Color=` attributes on `<MauiIcon>`
and `<MauiSplashScreen>` in the csproj. Delete `Resources/Images/dotnet_bot.png`
and its `<MauiImage Update=…>` line.

### Step 5 — build

```bash
dotnet build customers/Oab.Customer.Sami -f net10.0-windows10.0.19041.0
```

```bash
dotnet publish customers/Oab.Customer.Sami -f net10.0-android -c Release
```

See [08 — Build, Test & Release](08-build-test-release.md) for signing, which is
**not yet set up** — today's APKs are debug-signed and not shippable.

### Checklist

- [ ] Folder and csproj renamed
- [ ] `RootNamespace` + all namespaces updated
- [ ] **`ApplicationId` unique**
- [ ] `ApplicationTitle` set (Arabic name if that is what the shop wants)
- [ ] Added to `OAB.slnx`
- [ ] `ShopConfig` filled in
- [ ] Module list matches what they asked for, in the order they want the menu
- [ ] **`BackupModule` is in the list** — see [05 §5](05-modules.md#5-why-backup-is-effectively-mandatory)
- [ ] Icon and splash replaced; `dotnet_bot.png` gone
- [ ] Builds for Windows and Android
- [ ] Language switch tested in both directions
- [ ] A purchase logged, a payment made, a statement opened, a backup shared

## 3. `ShopConfig` reference

[`ShopConfig.cs`](../src/Oab.App/ShopConfig.cs)

### `ShopName` *(required)*

The window title, the flyout header, and the stem of every backup filename
(sanitized: invalid filename characters and spaces become `-`). Use the name the
shopkeeper would recognise, in their language.

### `CurrencySymbol` *(default `""`)*

Appended after every formatted amount, separated by one space: `1,250.00 د.ج`.
Empty means numbers only — appropriate when everyone in the shop already knows
the currency and the symbol is just noise.

### `DefaultCulture` *(default `"ar"`)*

The culture on first launch, before the user has ever pressed the language
button. After that, their saved choice wins (`IPreferences`, key `oab.culture`).
An invalid culture name falls back to `InvariantCulture` rather than crashing.

### `SupportedCultures` *(default `["ar", "en"]`)*

What the flyout footer button cycles through, in order. **Set it to a single
entry to effectively disable language switching** — `CycleCulture` does nothing
when fewer than two cultures are configured, so the button becomes inert. (It is
still visible; hiding it would need a shell change.)

### `UseArabicIndicDigits` *(default `false`)*

`true` renders amounts as `١٢٣.٠٠` instead of `123.00`. Affects money output
only. **Do not enable it for a shop that types amounts on an Arabic keyboard
until [the input gap](10-status.md#4-known-gaps-and-risks) is closed** — the app
can display those digits but cannot parse them back.

### `DatabaseFileName` *(default `"oab.db"`)*

The SQLite file name inside `FileSystem.AppDataDirectory` (the app's private
directory). There is rarely a reason to change it; every shop has its own APK
and therefore its own sandbox.

### `LabelOverrides` *(default empty)*

Per-shop wording, without code. See §5.

## 4. Choosing modules

| Shop says | Modules |
|---|---|
| "I just want to know what I owe the distributor." | `PurchasesModule`, `SupplierDebtsModule`, `BackupModule` |
| "Also who owes me." | + `CustomerDebtsModule` |
| "Everything." | all four (the template's default) |

Order in the `UseOab` argument list is the order of the flyout menu. Put the
screen they will open twenty times a day first.

## 5. Label overrides

The lookup order is: `"<lang>:<Key>"` → `"<Key>"` → resx → the key itself
([06 §1](06-localization.md#1-how-a-string-reaches-the-screen)).

```csharp
LabelOverrides = new Dictionary<string, string>
{
    // Arabic only: this shop calls the purchase log "the notebook"
    ["ar:Purchases_Title"] = "الدفتر",

    // Every language: they call suppliers "wholesalers"
    ["Suppliers_Title"] = "Wholesalers",

    // Reword a prompt to match how they actually ask it
    ["ar:Customers_DebtPrompt"] = "كم أخذ اليوم؟",
}
```

Notes:

- The prefix is the **two-letter ISO language name** — `ar`, `en`. Not `ar-SY`.
- Any of the 79 keys in [06 §4](06-localization.md#4-resource-key-catalogue) can
  be overridden, including flyout titles, empty-state text, prompts, error
  messages, and the backup summary headings.
- Overriding a key used in the summary report changes the **backup text file**
  too — the report takes its labels from `LocalizationManager`.
- An override for a key that does not exist is simply never consulted; there is
  no validation and no warning.

## 6. A shop wants something genuinely custom

Write it as a **new module inside that shop's customer folder**:

```
customers/Oab.Customer.Sami/
├── Oab.Customer.Sami.csproj
├── MauiProgram.cs
└── Custom/
    ├── ConsignmentModule.cs
    ├── ConsignmentPage.xaml(.cs)
    └── ConsignmentViewModel.cs
```

Implement `IOabModule` and pass an instance to `UseOab` alongside the shared
modules — the shell cannot tell the difference. Follow the conventions in
[05 §6](05-modules.md#6-writing-a-new-module).

**If a second shop later wants it**, move the code to
`src/Oab.Modules/Oab.Modules.Consignment/` as its own project, and reference it
from both heads. That promotion is the *only* time custom work moves toward the
core, and it is a file move plus two csproj lines.

**Custom code never touches `src/Oab.Core`, `src/Oab.Data`, or `src/Oab.App`.**
If a custom feature seems to need a core change, that is a signal the core is
missing a general capability — make the change general, with tests, rather than
shop-specific.

## 7. Where the customization thesis is still unproven

The architecture is designed so a second shop takes under an hour. **That has
not yet been measured**, because only the template head exists today. Building a
genuinely different second shop — different modules, different currency,
reworded, Arabic-Indic digits on — and *timing it* is the test of this whole
design. See [`ROADMAP.md`](ROADMAP.md), Week 4.

---

Next: [08 — Build, Test & Release](08-build-test-release.md)

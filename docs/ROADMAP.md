# OAB Roadmap — one month to a pilot-ready product

## The reframe

You can't checklist your way to hole-proof. What actually makes a shop ledger
trustworthy is four things, and only one of them is features:

1. **Data can't be lost.** Today one phone dies and the shop's entire book is
   gone — *worse than the notebook we're replacing*. This is the existential risk.
2. **Every number is explainable.** A shopkeeper who can't answer "why do I owe
   500?" stops trusting the app within a week. Trust is the product.
3. **Every mistake is fixable.** They will fat-finger amounts.
4. **A real shopkeeper has used it.** The core premise — "they skip POS because
   it's too much work, not price" — is still an untested assumption.

Two gaps found in the code that shaped this plan — **both now closed:**

- ~~`LedgerService.RecordAdjustmentAsync` exists but **no module calls it**~~ —
  closed: tapping an entry on the party statement corrects it.
- ~~`LedgerStore.GetEntriesForPartyAsync` exists but **no screen calls it**~~ —
  closed: the party statement page reads it.

The engine is ahead of the app. This month closes that gap.

**A third thing, learned the hard way in Week 1.** Both of those methods, once
wired up, turned out never to have run: SQLite cannot `ORDER BY` a
`DateTimeOffset`, and no test had ever called them against a real database. The
global exception handler found it within half a minute of being installed.
Assume, from here on, that **a store method with no real-SQLite test is not
written yet** — it is a guess that compiles.

**A fourth, from the first item of Week 2.** The amount parser was copied into
four screens, so it was untested four times over — nobody writes a test for a
private helper in a page's code-behind. The bug was not in what that code said;
it was in where it lived. **Logic duplicated across screens is untested by
construction.** When a rule appears twice, it belongs in Core, where a test costs
nothing. Both of Week 1's real bugs and this one share that shape.

---

## Week 1 — Make the ledger trustworthy

Nothing else matters if these are missing.

- [x] **Backup & restore module** — export the SQLite file plus a plain-text
  summary through the Android share sheet (WhatsApp to self / Drive). Prompt
  weekly. Restore from a received file. *Still to do: restore onto a different
  phone and confirm it.*
- [x] **Party statement screen** — tap a supplier/customer → entries with a
  running balance. Lives in `Oab.App/Views` rather than a module, because a
  party is often supplier and customer at once and both lists push the same
  page. Newest entry first, so the reason for today's balance is at the top.
  The pushing list passes its `PartyRole`, which is all that decides whether a
  balance shows red — red keeps meaning "the debt this screen is about is still
  open", the same as on the row that was tapped.
- [x] **Correction flow** — tap an entry → "Correct this entry" → what it should
  have been → why. Posts an `Adjustment` carrying the entry's `DocumentId`, so an
  invoice's outstanding follows the fix. The shopkeeper types a plain amount and
  never a sign: `LedgerMath.CorrectionDelta` takes the direction from the entry
  being corrected. Zero is a valid answer and means "this never happened". The
  reason is mandatory but pre-filled with `"Was 1,000.00 SP"`, so accepting it
  costs one tap. Tap rather than long-press because MAUI has no long-press
  gesture — and because the action sheet is the only thing that makes the feature
  discoverable. *Still to do: watch a real person get through three stacked
  dialogs on a phone.*
- [x] **Global exception handler** — two layers. `Page.RunSafelyAsync` (one
  extension method, not a base class, so a module can still hold a plain
  `ContentPage`) now wraps *every* `async void` handler and `OnAppearing` in the
  app and all four modules; the two private `RunAsync` copies are gone.
  Underneath it, `AppDomain` / `TaskScheduler` / Android / WinUI handlers
  installed in `UseOab` **before `UseMauiApp`**, so a migration crash in the
  `OabApp` constructor is covered too. Everything lands in a 64 KB
  `errors.log` the backup screen can share — that card is hidden until something
  has actually gone wrong. The handler records; it deliberately does not keep a
  crashed app alive, because a ledger in an unknown state can write a wrong
  number.
  **It paid for itself in 25 seconds:** the first launch after installing it
  logged a `NotSupportedException` proving that
  **the purchases list and the party statement had been failing on every open** —
  SQLite cannot `ORDER BY` a `DateTimeOffset`, and neither store method had a
  test against a real database. Both fixed, three regression tests added.

**Week 1 is complete.** All four items are done; two of them are still waiting on
a human (a restore onto a second phone, and a real person getting through the
correction flow's three dialogs).

## Week 2 — Real phone, real Arabic, real release

Everything so far is verified on Windows. That proves almost nothing about a
cheap Android phone in Arabic — and, as Week 1 discovered, "verified on Windows"
had been quietly untrue for two screens.

- [x] **Arabic-Indic digit input.** Closed first, because it was the one item on
  this list that could be finished without a phone in hand — and because it
  turned out to be worse than described. `MoneyInput.TryParseAmount` now lives in
  Core beside `MoneyFormat`, reads Arabic-Indic and extended Arabic-Indic digits,
  `٫` `٬` `،`, and bidi marks, and is the **only** amount parser in the codebase;
  the four private copies are deleted. 35 tests.
  **What the roadmap had wrong:** this was written as "if a user types ٥٠ it
  likely fails." The real scope is bigger — .NET's `ar` uses `٫` as its decimal
  separator and `٬` for grouping, so under Arabic the app was already *printing*
  separators no ASCII parser accepts, `UseArabicIndicDigits` or not. Fully
  configured, 1,250.50 renders `١٬٢٥٠٫٥٠` — no ASCII in it — and that is the
  string the correction flow pre-fills for a shopkeeper to retype. The screen for
  fixing wrong numbers was the one most likely to be handed a number it could not
  read.
- **Run on actual hardware.** Verify RTL layout, Arabic font rendering,
  `DatePicker` under `ar`, the numeric keyboard, and the decimal separator.
  **Then read `errors.log` from the backup screen**, whether or not anything
  looked wrong. That habit is the whole return on Week 1's last item.
  *Now also:* type `٥٠` into every amount box. The parser is unit-tested, but
  what the Arabic keyboard actually emits on a real device is not something a
  Windows test can tell you — if a character comes through that the table misses,
  the fix is one `case`.
- **Branding** — real icon, Arabic app name, per-shop `ApplicationId` (still
  `com.companyname.*`), version scheme.
- **Signed release APK** and documented keystore handling. Debug APKs aren't
  shippable.
- **Upgrade test** — install v1, enter real data, install v2 carrying a new
  migration, confirm nothing is lost. The `Database.Migrate()` promise is
  currently untested against real data.

**One of five.** The remaining four all need either a physical phone or a signing
key — there is no more of Week 2 that can be finished at a desk. The gating item
is getting the app onto hardware; branding and the APK can be done in parallel
with it, and the upgrade test needs the APK first.

## Week 3 — Fit the daily workflow, and scale

- **CashDay / today screen** — money in, money out, closing position. This is
  what they actually check at night.
- **Cut the taps.** Count taps to log a purchase; target ≤4. Recent-party
  shortcuts, remember last supplier, amount-first entry. This is the entire
  "too much work" thesis — it deserves deliberate design.
- **Fix the N+1 query** in `PurchasesListViewModel.LoadAsync`, which calls
  `GetEntriesForDocumentAsync` once per purchase inside the loop. Fine at 10
  purchases, painful at 2,000.
- **Seed 5,000 entries / 300 parties** and measure list load on a low-end
  device. Add search and archiving of settled items so lists stay short.

## Week 4 — Prove the customization thesis, then pilot

- **Build a genuinely different second shop** — Purchases + SupplierDebts only,
  different currency, reworded via `LabelOverrides`, Arabic-Indic digits on.
  **Time it.** More than an hour means the architecture isn't delivering its
  central promise, and that becomes the bug to fix.
- **Write the onboarding runbook** (or a `dotnet new` template) so shop #3 is
  mechanical.
- **Pilot with one real shopkeeper.** Put it on their phone. Don't explain it —
  watch where they hesitate. Fix the top three things they trip on, nothing else.

**Parallel track, starts now (zero code):** talk to 3–5 shopkeepers. Ask what
they'd want logged; watch them use their notebook. Costs nothing and could
redirect the whole month.

---

## Definition of "hole proof"

- [ ] Data survives a lost phone — *proven by restoring onto a different device*
- [x] Every balance taps through to the entries that produced it
- [x] Every mistake is fixable without rewriting history
- [ ] Works with zero internet, indefinitely
- [ ] Survives an app upgrade with real data
- [ ] Fully usable in Arabic on a cheap Android phone
- [ ] New shop → installed signed APK in under an hour
- [ ] Someone who has never seen it can log a purchase untaught

## Explicitly cut this month

Multi-device sync, cloud, barcode scanning, receipt printing, stock quantities,
reports beyond the day view, iOS. Each is "later, if a paying shop asks."

The differentiator is being **smaller** than a POS. Protect that.

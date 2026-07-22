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

Two gaps found in the code that shaped this plan:

- `LedgerService.RecordAdjustmentAsync` exists but **no module calls it** — a
  typo'd amount is permanent today.
- ~~`LedgerStore.GetEntriesForPartyAsync` exists but **no screen calls it**~~ —
  closed: the party statement page reads it.

The engine is ahead of the app. This month closes that gap.

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
- [ ] **Correction flow** — long-press an entry → "correct this" → post an
  `Adjustment` with its mandatory note. The statement page is where this
  belongs; it already labels adjustments "Correction" and shows their note.
- [ ] **Global exception handler** — the `async void` event handlers can crash
  silently. Log to a shareable file.

## Week 2 — Real phone, real Arabic, real release

Everything so far is verified on Windows. That proves almost nothing about a
cheap Android phone in Arabic.

- **Run on actual hardware.** Verify RTL layout, Arabic font rendering,
  `DatePicker` under `ar`, the numeric keyboard, and the decimal separator.
- **Arabic-Indic digit input.** `TryParseAmount` only tries CurrentCulture then
  Invariant — if a user types ٥٠ it likely fails. We render those digits but
  can't read them back.
- **Branding** — real icon, Arabic app name, per-shop `ApplicationId` (still
  `com.companyname.*`), version scheme.
- **Signed release APK** and documented keystore handling. Debug APKs aren't
  shippable.
- **Upgrade test** — install v1, enter real data, install v2 carrying a new
  migration, confirm nothing is lost. The `Database.Migrate()` promise is
  currently untested against real data.

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
- [ ] Every mistake is fixable without rewriting history
- [ ] Works with zero internet, indefinitely
- [ ] Survives an app upgrade with real data
- [ ] Fully usable in Arabic on a cheap Android phone
- [ ] New shop → installed signed APK in under an hour
- [ ] Someone who has never seen it can log a purchase untaught

## Explicitly cut this month

Multi-device sync, cloud, barcode scanning, receipt printing, stock quantities,
reports beyond the day view, iOS. Each is "later, if a paying shop asks."

The differentiator is being **smaller** than a POS. Protect that.

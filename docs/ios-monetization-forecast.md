# iOS Monetization Forecast — Farkle

> **Status:** Analysis / forecast only. Nothing in this document is implemented. It is a
> strategic assessment of *how* this app could earn revenue on iOS, *what it would cost to
> get there*, and *what to realistically expect*.
>
> **Prepared:** June 2026. App Store commercial terms are in active legal flux (see
> [§3](#3-the-2026-ios-commission-landscape)) — revisit the rates before committing.

---

## 1. TL;DR

- **Farkle today is a web-only, free, non-monetized game.** It's a Blazor Server host + Blazor
  WASM client with real-time multiplayer (SignalR), ASP.NET Identity accounts, and an
  event-sourced backend on Azure. There is **zero** payment, subscription, ads, or IAP code in
  the repository.
- **Getting onto iOS is the first cost, not monetization.** The fastest credible route that
  reuses the existing Razor/C# code is a **.NET MAUI Blazor Hybrid** shell wrapping the existing
  components. A PWA is cheaper but cannot be listed on the App Store and has weaker monetization
  hooks.
- **Best-fit revenue model for a casual dice game is *hybrid*:** rewarded/interstitial ads for
  the free majority + a low-priced **"Pro" subscription / remove-ads + cosmetics** IAP for the
  paying minority. Real-money tournaments are *possible* but carry serious gambling-law risk —
  treat as out of scope for v1.
- **Realistic revenue is modest** without a user-acquisition budget. An illustrative *base case*
  (~50k downloads/yr, ~3k MAU) nets roughly **$15k–25k/yr** after Apple's commission and infra.
  This is a portfolio/learning-grade outcome, not a business, until growth is funded.
- **Commission:** qualify for Apple's **Small Business Program** → **15%** from day one (vs. the
  standard 30%). In the US, you may now also **link out to external payment** (Stripe/web) — but
  Apple is litigating a "reasonable commission" on those links, so the 0% loophole is not stable.

---

## 2. Starting point — what exists and what's missing

Grounded in the current codebase (see file references at the end):

**What the app does today**
- Full Farkle game loop: start game → players join lobby → roll / keep / pass → **first to 5,000 wins**.
  Single-player games are now allowed (minimum players lowered to 1).
- **Real-time multiplayer** via SignalR (`/hubs/game`): turn changes *and* live rolls/keeps are
  broadcast to all players, so **off-turn players spectate the live table**.
- **Share-to-join**: lobby exposes a **QR code + share link/button** (`IShareService`, `JoinQrCode`)
  for pulling friends into a game — a built-in virality hook (see §6/§9).
- **Tap-to-select dice** UI (replaced the earlier drag-and-drop); dice still render via CSS-3D with
  `IRotationCalculator` (the cosmetics surface is intact).
- **Accounts**: ASP.NET Identity (email/password) + PostgreSQL, JWT bearer tokens (4-hour expiry).
- **Event-sourced** backend (Eventuous + EventStore) with a **CQRS `GameView` read-model projector**
  (`GameViewProjector`, `IGameViewStore`) deployed to Azure Container Apps.

**What's missing for monetization** (all confirmed absent from the repo)
| Capability | Present? | Needed for |
|---|---|---|
| Native iOS app / MAUI / Capacitor / PWA manifest | ❌ | App Store listing at all |
| StoreKit 2 / in-app purchase | ❌ | Selling anything through iOS |
| Ad SDK (AdMob, AppLovin MAX, etc.) | ❌ | Ad revenue |
| Subscription / entitlement model in Identity | ❌ | "Pro" tiers, ad removal |
| Sign in with Apple | ❌ | Required if any third-party social login is added |
| Receipt/subscription validation server-side | ❌ | Anti-fraud, cross-device entitlement |

**Implication:** monetization is a *greenfield* addition. The good news is the architecture is
clean — entitlements map naturally onto JWT claims + an Identity schema extension, and the event
sourcing model already gives you the telemetry backbone for monetization analytics.

---

## 3. The 2026 iOS commission landscape

This directly shapes "take-home per dollar," so it's worth getting current.

- **Standard commission:** 30% on paid apps, IAP, and first-year subscriptions; 15% on
  subscriptions after a subscriber's 12th month.
- **Small Business Program:** **15% from day one** on paid apps and IAP for developers earning
  **< $1M/yr**. A solo/indie Farkle almost certainly qualifies. **Assume 15% throughout this
  forecast.** ([Apple](https://developer.apple.com/app-store/small-business-program/),
  [RevenueCat](https://www.revenuecat.com/blog/engineering/small-business-program/))
- **External payment links (US):** Following the *Epic v. Apple* contempt ruling, Apple updated
  Guidelines 3.1.1/3.1.3 to allow external-payment links/buttons in US apps. As of June 2026 the
  Supreme Court declined to stay the order, so link-outs remain allowed — **but** the Ninth
  Circuit said Apple may charge a "reasonable commission" tied to its costs, and the exact rate
  is still being set by the district court. **Do not build a business model that depends on the
  0% external-link loophole surviving.**
  ([MacRumors](https://www.macrumors.com/2025/12/11/apple-app-store-fees-external-payment-links/),
  [TechCrunch](https://techcrunch.com/2026/04/29/apple-epic-games-app-store-fees-pause-changes-supreme-court/),
  [RevenueCat](https://www.revenuecat.com/blog/growth/apple-anti-steering-ruling-monetization-strategy/))
- **EU (DMA):** alternative app marketplaces and external payment are permitted under separate EU
  terms (Core Technology Fee considerations apply); Small Business Program subscriptions after
  year 1 can drop to 10%. Relevant only if you target the EU specifically.

**Planning rate:** model **15% Apple commission** as the base case; treat any external-link
savings as upside, not foundation.

---

## 4. Getting onto iOS — distribution options

You cannot monetize on iOS until you ship *something* installable. Ranked by fit for this codebase:

| Option | Reuses Razor/C#? | App Store listing | IAP / StoreKit | Ads | Effort | Notes |
|---|---|---|---|---|---|---|
| **.NET MAUI Blazor Hybrid** ✅ *recommended* | Yes — `BlazorWebView` hosts existing components | Yes | Yes (via plugin/RevenueCat) | Yes | Medium | Best code reuse; native shell; full monetization surface. |
| **PWA (add manifest + service worker)** | Yes | **No** | No (web only) | Web ads only | Low | Cheapest, but no App Store presence, weak push, no StoreKit. Good as a free web channel, not an iOS revenue play. |
| **WKWebView wrapper (Capacitor / custom)** | Yes (loads the web app) | Yes | Possible but awkward | Possible | Medium | Apple scrutinizes thin web wrappers (Guideline 4.2); needs native value-add. |
| **Native Swift rewrite** | No | Yes | Best-in-class | Best | High | Throws away the C# investment; not justified. |

**Recommendation:** **MAUI Blazor Hybrid.** It keeps the domain, UI components, and API client,
gives a real App Store binary, and unlocks StoreKit + ad SDKs. Use **RevenueCat** (or
[.NET StoreKit bindings](https://learn.microsoft.com/dotnet/ios/)) to abstract receipt validation
and cross-platform entitlements so you don't hand-roll Apple receipt verification.

A pragmatic two-channel play: **ship the PWA now** (cheap, free web growth + wishlist/funnel) and
**MAUI Blazor Hybrid** as the monetized iOS product.

---

## 5. Monetization models — fit for a casual dice game

Ranked by fit and risk for Farkle specifically:

### 5.1 Hybrid (ads + IAP) — **recommended primary**
The dominant model for casual/board games. Free to play, monetize the majority through ads and
the minority through purchases.
- **Rewarded video** (best fit): "watch an ad for a re-roll / mulligan / extra turn / daily bonus
  currency." High opt-in rates, low intrusiveness.
- **Interstitial** between matches (cap frequency to protect retention).
- **Banner** in lobby/scoreboard (low yield, low risk).

### 5.2 "Pro" subscription / Remove-Ads — **recommended secondary**
- One-time **Remove Ads** IAP (~$2.99–4.99) — the single highest-converting purchase in ad-funded
  casual games.
- Or a **Pro subscription** (~$2.99–3.99/mo) bundling: no ads, private/ranked rooms, persistent
  stats & match history (the event sourcing + new **`GameView` CQRS read model** make this nearly
  free to build), custom game rules (target score, table size), and cosmetics.

### 5.3 Cosmetic IAP — **good fit, low risk**
Dice skins, table/felt themes, pip styles, win animations, avatars. The app already renders dice
via CSS 3D + an `IRotationCalculator`, so skins are a natural, non-pay-to-win extension. Cosmetics
are safe (no gambling/fairness concerns) and align with Apple's preferences.

### 5.4 Soft currency / consumables — *optional*
Coins for re-rolls/cosmetics, sold in packs. Adds complexity; only worth it once retention is proven.

### 5.5 Real-money tournaments / wagering — **❌ out of scope for v1**
Entry-fee tournaments are the highest-ARPU model in dice games **but** Farkle is partly
chance-based, so real-money play risks classification as **gambling** — triggering Apple
Guideline 5.3, state-by-state US skill-vs-chance law, licensing, KYC/AML, and geo-restrictions.
Not worth it for an indie project without legal counsel. Keep tournaments **cosmetic/bragging-rights
only**.

**Pay-to-win caution:** never sell scoring advantages in a competitive multiplayer game — it
destroys the core loop and invites refunds/reviews. Keep purchases cosmetic or convenience-only.

---

## 6. Illustrative revenue forecast

> **These are scenario models built on public casual-game benchmarks, not predictions.** Actual
> results depend on retention, geography, and user-acquisition spend (assumed **$0 paid UA** here —
> organic only). Treat the *shape* and *ratios* as the takeaway, not the absolute dollars.

**Assumptions (hybrid model):**
- Organic-only acquisition, but the app now ships a **QR/share-to-join** invite hook, which gives a
  modest built-in **virality (k-factor)** tailwind on the download assumptions below — multiplayer
  invites are the cheapest growth channel this app has.
- Ads: blended **ARPDAU ≈ $0.06** (rewarded-heavy hybrid casual).
- IAP (cosmetics + remove-ads): **~2%** of MAU pay, **~$8 ARPPU/yr**.
- Subscription "Pro": **~2%** of MAU at **$3.99/mo**.
- Apple commission: **15%** (Small Business Program). Infra: **~$1.5k–3k/yr** (per the Azure
  estimate in §7). Apple Developer Program: **$99/yr**.

| Scenario | Downloads/yr | MAU | DAU | Ads gross | IAP gross | Subs gross | **Gross/yr** | **Net/yr*** |
|---|---|---|---|---|---|---|---|---|
| **Conservative** | ~5,000 | 300 | 80 | $1,750 | $480 | $290 | **~$2,500** | **~$0–1k** |
| **Base** | ~50,000 | 3,000 | 800 | $17,500 | $5,800 | $2,900 | **~$26,000** | **~$18–20k** |
| **Optimistic** | ~500,000 | 25,000 | 6,000 | $131,000 | $48,000 | $24,000 | **~$203,000** | **~$170k** |

\* Net = gross − 15% Apple − infra − $99. Conservative net is ~break-even (infra eats the revenue
at low scale — a real risk: **a tiny user base can cost more to host than it earns**).

**What the model says:**
1. **Ads dominate volume; IAP/subs dominate margin.** A hybrid model is the right hedge.
2. **Scale is everything.** The jump from Conservative→Optimistic is ~80×, driven entirely by
   downloads/retention — i.e., by product quality and (eventually) UA spend, not by pricing.
3. **At low scale you can lose money** because of the always-on event-store + Postgres backend.
   Consider scale-to-zero / cheaper hosting (see §7) before launch.
4. **Without paid UA, expect Conservative-to-low-Base.** The Optimistic column effectively
   requires marketing investment or virality.

---

## 7. Engineering work & cost to enable monetization

Phased, smallest-viable-first:

**Phase 0 — iOS presence (prerequisite)**
- Add **MAUI Blazor Hybrid** head project reusing `WebApp.Client` components.
- Apple Developer Program enrollment ($99/yr), provisioning, App Store Connect listing.
- Backend reachable from device (already HTTPS via Container Apps).

**Phase 1 — entitlements backbone**
- Extend `AppUser`/Identity schema: `subscription_status`, `tier`, `entitlements`, `apple_original_transaction_id`.
- Add entitlement claims to JWT; gate features server-side (don't trust the client).
- **Sign in with Apple** (mandatory once any social login exists; recommended regardless for iOS UX).

**Phase 2 — purchases**
- Integrate **RevenueCat** (recommended) or StoreKit 2 directly for IAP + subscriptions.
- Server-side receipt/notification validation (App Store Server Notifications v2) → flip entitlements.
- Build the **Remove Ads** + **cosmetics** catalog first (simplest, highest ROI).

**Phase 3 — ads**
- Integrate **AppLovin MAX** or **AdMob** mediation; implement **rewarded** placements first.
- Respect the entitlement flag (no ads for Pro/Remove-Ads users).
- App Tracking Transparency (ATT) prompt + privacy nutrition labels.

**Phase 4 — cosmetics & retention**
- Dice/table skins (extends existing CSS-3D dice), persistent stats/match history (cheap given
  event sourcing), daily rewards.

**Infra cost reality (current Azure setup ≈ $105–200/mo).** Before monetizing, reduce idle cost:
prefer **scale-to-zero** Container Apps, evaluate a managed ESDB or consolidating the event store,
and right-size Postgres (Burstable B1ms). At Conservative scale, infra is the dominant cost line —
fix this first or the unit economics are negative.

---

## 8. Key risks & gotchas

- **Apple Guideline 4.2 (thin wrappers):** a bare WebView wrapper risks rejection. MAUI Blazor
  Hybrid with native integration (StoreKit, push, ATT) is much safer.
- **Gambling law (5.3 / skill-vs-chance):** keep all competition cosmetic; no entry fees / payouts in v1.
- **External-payment commission is unsettled** (§3) — don't build margins around 0% link-outs.
- **Low-scale infra cost > revenue** — the single biggest practical risk for an indie launch.
- **Pay-to-win backlash** in competitive multiplayer — keep purchases cosmetic/convenience.
- **Privacy/ATT:** ad monetization requires ATT prompts, SKAdNetwork/AdAttributionKit setup, and
  accurate privacy labels, or you risk rejection and degraded ad rates.
- **Refunds & subscription churn:** model first-year churn; subs only beat one-time purchases if
  retention holds past a few months.

---

## 9. Recommendation

1. **Decide intent.** If this stays a portfolio/learning project, monetization is a *feature
   showcase* (StoreKit + entitlements + ads), not a revenue plan — build the smallest hybrid slice
   and move on. If it's a real product, the gating factor is **growth/UA**, not the tech — and the
   existing **QR/share-to-join** invite loop is the cheapest growth asset to lean on first
   (instrument it, then optimize invite→install conversion).
2. **Ship the cheap channels first:** PWA for free web reach; MAUI Blazor Hybrid for the iOS binary.
3. **Monetize in this order:** Remove-Ads IAP + cosmetics → rewarded ads → optional Pro
   subscription. Skip soft currency and real-money tournaments for v1.
4. **Enroll in the Small Business Program** (15%) and plan around it; treat US external-payment
   savings as upside.
5. **Fix infra unit economics (scale-to-zero) before launch** so a small audience doesn't run at a loss.
6. **Re-check Apple's commission terms** (the *Epic* remedy is mid-litigation) before finalizing pricing.

---

### Codebase references
- Game domain (winning score = 5,000): `src/Farkle/Domain/GameAggregate/Game.cs`
- CQRS read model (stats/match-history surface): `src/Farkle/Application/GameViewProjector.cs`, `IGameViewStore.cs`
- API endpoints: `src/Farkle/Endpoints/`
- Auth / Identity (extend for entitlements): `src/WebApp/Auth/`
- WASM client (reuse in MAUI head): `src/WebApp.Client/`
- Dice rendering (cosmetics surface): `src/WebApp.Client/Pages/Game/Components/DiceTray.razor`, `Die.razor` + `Services/RotationCalculator`
- Share-to-join (virality hook): `src/WebApp.Client/Services/ShareService.cs`, `Pages/Game/Components/JoinQrCode.razor`
- Infrastructure & hosting cost: `infra/main.bicep`, `infra/modules/workload.bicep`

### Sources (external, 2026)
- [Apple — App Store Small Business Program](https://developer.apple.com/app-store/small-business-program/)
- [RevenueCat — The 15% App Store Fee (2026)](https://www.revenuecat.com/blog/engineering/small-business-program/)
- [MacRumors — Apple wins ability to charge fees on external payment links (Dec 2025)](https://www.macrumors.com/2025/12/11/apple-app-store-fees-external-payment-links/)
- [TechCrunch — Apple loses bid to pause App Store fee changes (Apr 2026)](https://techcrunch.com/2026/04/29/apple-epic-games-app-store-fees-pause-changes-supreme-court/)
- [RevenueCat — What the anti-steering ruling means for monetization](https://www.revenuecat.com/blog/growth/apple-anti-steering-ruling-monetization-strategy/)

> Benchmark ranges (ARPDAU, IAP conversion, subscription take-rate) reflect publicly reported
> casual/hybrid-casual game norms and are used here only to illustrate scenario shape.

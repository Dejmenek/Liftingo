# Liftingo: Product Requirements Document

**Intelligent strength-training assistant**

| | |
|---|---|
| Document version | 2.0 (supersedes *FitMentor AI — Dokumentacja biznesowa* v1.0, May 2026) |
| Date | 6 September 2026 |
| Status | Draft, pending section-by-section review |
| Language | English (source of truth); Polish translation to follow after review |

---

## 0. Change log vs. v1.0

| # | Change | Impact on document |
|---|---|---|
| 14 | **Twitter (X) sign-in deferred** (spec 001-user-auth clarifications, 14 Oct) | Google and Facebook remain; X moved to a later feature because it issues no identity token the client can hand to the API (US-01, US-24, FR-01). The consent checkboxes for a provider sign-up can be shown before the provider sign-in or after it (US-01) |
| 13 | **Authentication hardening** (spec 001-user-auth clarifications, 12 Oct) | Provider e-mails trusted, with pre-hijacking protection on linking (US-01, FR-01); sign out of all devices (US-24, FR-02); per-address e-mail limit for reset and verification (US-01, US-24, FR-01, FR-03); login log widened to a security audit log (US-24, NFR-17, US-18, US-37, retention table); a password reset ends every session, not only the other ones (US-24); known-device and e-mail-token rows added to the retention table (NFR-40) |
| 12 | **External sign-in providers changed** (spec 001-user-auth, 12 Oct) | Google/Apple replaced with Google, Facebook and Twitter (X) in US-01, US-24, US-18 and FR-01; welcome-only e-mail for provider-verified sign-ups (US-01, FR-01); new-device sign-in e-mail (US-24, FR-02, NFR-41); known devices added to export and deletion (US-18, US-37) |
| 1 | Product renamed from **FitMentor AI** to **Liftingo** | Name replaced throughout; diagram files and Mermaid titles to be updated separately |
| 2 | Explicit compliance with **GDPR (RODO)** and the **EU AI Act** | New section 4.5 (Legal & Compliance) with AI Act requirements NFR-27 – NFR-33; new user story US-22 |
| 3 | **Progression suggestions are rule-based only** (no ML model in v1) | FR-10 rewritten; `ProgressionAdvisor` is now an expert-rules engine; ML-based progression moved to P2 / Future Considerations; section 2.3 rewritten |
| 4 | **Premium tier removed**, single free product | Section 2.8 (business model) removed; all "premium" limits removed from FR-07, FR-28, FR-31 and user stories; goals and success metrics no longer reference conversion |
| 11 | **NFR cleanup** (review pass, 6 Sep) | Dropped uptime/concurrency NFRs; Polish-only UI; added NFR-39/40 (AI limits, retention) and NFR-41 (notifications); NFR-17/24 promoted to P0, NFR-16 demoted to P2; AI Act rows reworded for thesis scope |
| 10 | **Priority pass** (review pass, 6 Sep) | Six FRs demoted to P1, two promoted to P0; priority levels redefined as thesis scope |
| 9 | **Body weight required** (review pass, 6 Sep) | Health-data consent mandatory at registration; body-data step after questionnaire; withdrawal = account deletion (US-01, US-02, US-25, NFR-22) |
| 8 | **Cardio & mobility support** (review pass, 6 Sep) | New section 2.10, FR-45/46, US-23; generator adds mobility warm-up + optional cardio days; counts toward streak, not leaderboards |
| 7 | **Reframed as engineering thesis** (review pass, 6 Sep) | Goals rewritten as verification criteria (section 3); section 8 replaced by thesis verification table; market targets removed |
| 6 | **Health posture defined** (review pass, 6 Sep) | Single yes/no restriction question, disclaimer, no health-condition data (FR-43); injury-aware plans deferred (FR-44) |
| 5 | **Friends and friend leaderboards** added | New module "Social" (section 2.8); new requirements FR-32 – FR-40; new user stories US-19 – US-21; new privacy requirements NFR-34 – NFR-36 |

Requirement IDs from v1.0 are preserved wherever the requirement still exists, so the existing use-case, class and activity diagrams remain traceable. Removed or changed IDs are marked in the tables.

---

## 1. Problem Statement

Beginners in strength training (people with **under 12 months of training experience**, or **returning users** who have been away from regular training for **6 months or more**) mostly track workouts on paper or in spreadsheets, whether they train at a commercial gym or at home with basic equipment. These tools give no insight into progress, no guidance on when and how much to increase load, no way to recover a plan after missed sessions, and no feedback loop that keeps motivation alive. Most beginners also train alone: nobody notices when they skip a week, and there is no one to measure themselves against. The result is stagnation, injury risk from guessed progressions, and a high dropout rate within the first months.

Liftingo combines a fast mobile workout log with an AI plan generator, rule-based progression advice, automatic plan adaptation and a social layer (friends who are on the same journey, whose progress you can see and want to beat), so that a beginner can start training with a sound plan within minutes and keep going because the app, and their friends, are paying attention.

Not solving this leaves the beginner segment served either by generic logging apps (no guidance, no accountability) or by expensive personal coaching, and leaves our product without a defensible differentiator on the Polish market.

**Evidence.** A review of app-store descriptions, independent app reviews and comparison articles for the main competitors (Strong, Hevy, Fitbod, JEFIT) supports each element of the problem statement above:

- **No progression guidance in plain loggers.** JEFIT's free tier is widely reported as offering little guidance and being unsuitable for beginners, with reviewers specifically citing its lack of long-term planning guidance. Strong and Hevy are consistently described by reviewers as trackers, not planners: the standard advice is "use Fitbod if you want the app to plan your training; use Strong or Hevy once you already have a plan."
- **The gap is commercially recognised.** A cluster of small, single-purpose apps (OneMoreRep, Alpha Progression, StepLoad, Milo of Croton, "Progressive Overload Workout") exist solely to tell a lifter when to add weight; their marketing language ("no more guesswork," "tells you when to increase weight") is effectively the problem statement restated by other developers.
- **Plan rigidity confirmed.** An independent comparison names "the limitation both apps [Hevy and Strong] share when your session goes off plan": neither adapts to a missed or off-plan session, which is exactly the gap FR-09 addresses.
- **Social accountability is a validated retention lever, not a guess.** Reviews repeatedly credit Hevy's friend/social feed with providing accountability "that Strong completely lacks," and describe it as the main reason some users stay consistent, direct support for the social rationale added to this problem statement.
- **Injury handling is a known, unmet need.** An independent Fitbod review states the app has no injury filter and that this is its most-requested missing feature, which supports treating injury-aware plan generation as a real but deferred need (FR-44, P2) rather than ignoring it.
- **No free product currently combines planning, progression and social for beginners.** AI-planning apps (Fitbod) are paid after a short trial; plain loggers are free but unguided. A free, beginner-first product spanning both is not clearly occupied.

This is desk research (app-store listings, published reviews and comparison articles as of September 2026), not primary user interviews; it is sufficient to ground the problem statement for a thesis but should be named as such. Sources are listed at the end of this document.

## 2. Product Overview

### 2.1 Target users

**Definitions**

- **Beginner**: fewer than 12 months of regular strength training.
- **Returning user**: previously trained regularly but has been away for **≥ 6 months**. Treated as a beginner for plan generation, with the questionnaire capturing prior experience.
- **Training environment**: both **commercial gym** and **home** (bodyweight, dumbbells, resistance bands, optional bench/bar) are first-class. The exercise library, questionnaire and plan generator must support each environment fully: a home-only user gets a complete plan, not a stripped-down gym plan.
- **Health posture (v1)**: Liftingo is designed for adults who can train without medical restrictions. The app does **not** collect information about injuries or medical conditions. Onboarding asks a single yes/no question ("Can you train without restrictions from injury or a medical condition?"), and users who answer "no" receive a mandatory disclaimer and are advised to consult a professional, but are not blocked. Plans are not adapted to injuries in v1 (see FR-43, Non-Goals, OQ-14 resolved).

**Primary persona: Tomek, 27.** Junior developer, three months into gym training. Doesn't know whether his technique is right, doesn't know how to progress, gets discouraged easily, trains alone with an Android phone in hand.

**Secondary persona: Kasia, 24.** Marketing coordinator, started training four months ago with two friends from work. Two of them go to the gym after work; the third mostly trains at home with dumbbells. Kasia's motivation comes from the group: she wants to see that her friends trained this week, compare who is progressing faster, and not be the one who fell behind. She's the one who would invite the others into the app.

Segment characteristics:

- Age 18–40, concentrated in 22–32. **The product is for adults only (18+).**
- Digitally fluent, expects a modern mobile-first interface.
- Limited domain vocabulary (does not know RPE, tempo, periodization).
- Wants to be guided step by step and to get immediate feedback.
- Low tolerance for friction: every confusing interaction increases churn risk.
- Often has 1–3 friends or colleagues who train at the same time; social accountability is a lever, not a nice-to-have.

Intermediate and advanced lifters are out of scope for v1 (see Non-Goals).

### 2.2 Functional modules

| Module | Description |
|---|---|
| Account & Profile | Registration, login, personal data, training preferences, consents |
| AI Plan Generator | Personalised 4–12-week plans from goal, experience and equipment |
| Workout Log | Fast logging of sets, reps and weight during a session, with rest timer and offline mode |
| Exercise Library | Small, fully specified library (45–55 entries, six exercise types) with technique text, media and progression metadata; favourites and custom exercises |
| Statistics & Progression | Progress charts, personal records, **rule-based** load progression suggestions |
| **Cardio & Mobility (new)** | Logging of cardio and mobility sessions; warm-up/mobility and optional cardio days scheduled by the plan generator |
| Plan Adaptation | Automatic plan modification after skipped sessions or goal change |
| Gamification & Goals | Badges, levels, streaks and personal goals |
| **Social (new)** | Friends, activity visibility controls and friend leaderboards |

### 2.3 Progression advice: rule-based (changed)

In v1 the progression engine is a deterministic **expert-rules system**. It does not use a trained model, does not learn from user data and produces the same output for the same input. Rules are derived from established strength-training literature (e.g. double progression, load increments by exercise type, deload after repeated failures) and are versioned and reviewable.

Rationale: the ML-based approach from v1.0 needs research on data volume, validation and AI Act classification before it can be safely shipped. A rules engine is explainable by construction, which also satisfies the transparency requirements in section 4.5.

The **AI plan generator** (section 2.4) remains an AI component and is subject to the AI Act requirements in section 4.5.

### 2.4 AI plan generation

After a short onboarding questionnaire (goal, days per week, available equipment, experience) the system generates a 4–12-week plan with exercises, sets, reps and starting weights. Every generated plan passes a deterministic **methodology validator** (muscle-group rotation, recovery days, weekly volume) before it is shown to the user. The user is clearly informed that the plan is AI-generated and can request an alternative variant, edit the plan manually, or decline it.

### 2.5 Plan adaptation

When a planned session is not logged within 24 h, the system offers to reschedule the session, redistribute the volume across upcoming sessions, or continue as planned. After a break of 14 days or more it proposes a shortened comeback plan. The user's choice is remembered per session.

### 2.6 Workout log and exercise library

The log is optimised for one-handed use at the gym: large targets, three taps or fewer to save a set, automatic rest timer, full offline operation with background sync. The library is deliberately small at launch (45–55 entries) but complete: every entry carries the full data model in Appendix B, so the generator, progression rules, substitution and filters all work from the same fields and new exercises are pure data additions. Six exercise types are supported (weighted, bodyweight, band, timed hold, cardio, mobility), each with its own logging widget and progression branch.

### 2.7 Statistics, calendar and export

Charts of max load and volume per exercise, aggregate statistics, estimated 1RM, personal records, muscle-group distribution, a monthly/weekly calendar, and export to CSV and PDF (both available to all users).

### 2.8 Social: friends and leaderboards (new)

Users can connect with friends (by username, e-mail or invite link) and compete on opt-in weekly leaderboards (workouts completed, total volume, current streak). Social features are **off by default**: nothing about a user is visible to anyone until the user explicitly enables a social profile and accepts a friend request. Leaderboards show only aggregated metrics the user has chosen to share, never individual sets, body weight or profile details.

### 2.10 Cardio and mobility (new)

Liftingo is strength-first but supports two additional activity types:

- **Cardio** (running, cycling, rowing, walking, elliptical, swimming, other): logged as duration + optional distance + RPE. No sets/reps.
- **Mobility** (stretches, foam-rolling, dynamic warm-ups): logged as duration or number of holds.

Any session can mix strength, cardio and mobility blocks. The plan generator adds a short mobility warm-up to every strength session and, depending on the user's goal and available days, schedules **optional** cardio days (marked as optional; skipping them does not trigger plan adaptation). Cardio and mobility count toward the training streak (any completed session counts) but **not** toward friend leaderboards, which remain strength-based. Rule-based progression applies to strength only.

### 2.9 Business model (changed)

Liftingo is a **single free product** with no paid tier and no feature gating. There are no plan-generation quotas, goal limits or export restrictions tied to a subscription. Any operational limits (e.g. rate limits on AI generation to control cost) are fair-use limits applied equally to all users and are listed in the requirements.

---

## 3. Goals

**Project context.** Liftingo is developed as an **engineering thesis project**. Its goals are therefore verification criteria that can be demonstrated in the thesis, not market targets. Monetisation, user acquisition and retention economics are out of scope by design.

**User-facing goals** (each demonstrable through testing)

1. **Fast start**: a new user reaches an active, personalised plan within **5 minutes** of registration (measured in a usability test with ≥ 5 testers, median time).
2. **Frictionless logging**: saving a set takes **≤ 3 taps** and completes in **≤ 2 s**, online and offline (measured by UI count and performance test).
3. **Guided progression**: users who complete ≥ 4 weeks of a plan see a measurable increase in estimated 1RM on their main lifts, and every planned strength exercise with ≥ 2 logged performances shows a rule-based suggestion with a rationale.
4. **Environment parity**: a home-only user and a gym user both receive a complete plan and the same feature set; no flow is gym-only.
5. **Social accountability works technically end-to-end**: two test users can connect, opt in, and see each other on a weekly leaderboard with privacy controls respected.

**Engineering / thesis goals**

6. **Functional completeness**: all P0 requirements implemented and passing acceptance criteria. P0 is the thesis deliverable; P1 items are implemented if time allows; P2 items are designed for (interfaces, data model) but not built.
7. **Compliance by design**: GDPR (RODO) and AI Act obligations are implemented and documented via a self-assessment checklist with zero open items at submission.
8. **Explainable AI boundary**: the thesis can clearly demonstrate which outputs are AI-generated (plan generator) and which are deterministic (rule engine, validator), with logging to prove it.

## 4. Non-Goals

| Non-goal | Rationale |
|---|---|
| Machine-learning-based progression or personalisation | Requires research on data volume, validation and AI Act risk classification; deferred to a separate initiative (P2) |
| Paid subscription / premium tier / in-app purchases / any monetisation | Removed by product decision; out of scope for a thesis project |
| Nutrition tracking, wearable integrations | Separate initiatives; not needed for the core problem |
| Content for intermediate/advanced lifters (periodisation, RPE-driven autoregulation) | Different persona and vocabulary; would dilute the beginner-first UX |
| Public/global leaderboards, public profiles, in-app chat, feed or comments | Substantially raises moderation, safety and GDPR burden; v1 social is friends-only and metrics-only |
| Users under 18 | Health-adjacent data plus social features for minors would require parental consent flows and additional safeguards |
| Injury-aware plan generation (excluding/substituting exercises for a specific injury) | Would require processing health-category data and hardens the AI Act classification question; deferred to P2 (FR-44) pending legal review |
| Coach / trainer accounts (a PT viewing or managing a client's plan) | Separate role model and permissions; plausible future request but large scope |
| Sharing workouts to external platforms (Instagram/Strava-style cards) | Growth feature with privacy implications; not the friends-based model chosen |
| Live / real-time co-training (seeing a friend's session as it happens) | Leaderboards are weekly aggregates; real-time presence is a different product |
| Cardio progression suggestions and cardio-specific plans (e.g. 5K programme) | Cardio and mobility are **logged and scheduled** (see 2.10) but only strength gets rule-based progression in v1 |
| Native iOS/Android apps | v1 ships as a PWA (NFR-24); see OQ-15 on iOS timer/vibration limits |

---

## 5. User Stories

Format: *As a [user], I want [capability] so that [benefit].* Acceptance criteria are listed per story. Priority order within each group.

### 5.1 Onboarding and account

**US-01: Registration** *(FR-01, NFR-21, NFR-22)*
As a new user, I want to create an account with e-mail, Google or Facebook so that I can start without a long form.

- Registration form contains: e-mail, password, and three unticked checkboxes: (a) "I am 18 or older and accept the Terms and Privacy Policy" (required), (b) explicit consent to process health-category data (body weight, height and body measurements) with a one-line explanation that it is needed to compute starting loads (required), (c) marketing e-mails (optional).
- Registration is blocked if (a) or (b) is not ticked; the message for (b) explains why body weight is needed and that the account cannot be created without it.
- Google and Facebook sign-in buttons are available. A new user ticks the same three checkboxes, either before the provider sign-in (registration page) or right after it (when they started from the login page); the account is not created until (a) and (b) are accepted.
- A verification e-mail with a single-use link (valid 24 h) is sent. Until verified, the user can complete the questionnaire and use the app, but a persistent banner asks for verification; social features (FR-32) and data export (FR-56) are unavailable and AI generation is limited to 1 per 24 h (NFR-39). Unverified accounts are deleted after 7 days.
- The user can request a new verification e-mail; at most 3 are sent per address per hour, and requests over the limit get the same response without an e-mail.
- An e-mail address supplied by Google or Facebook is treated as verified, and the account skips verification. If it matches an existing account, the provider is linked automatically. If that account's e-mail was never verified, linking also removes its password and signs out all its sessions, so whoever registered the address without proving ownership loses access (protection against account pre-hijacking).
- The verification e-mail doubles as the welcome e-mail. If the external provider confirms the e-mail as verified, no verification is needed and a welcome-only e-mail is sent instead. Every user receives exactly one welcome message.

**US-02: Onboarding questionnaire** *(FR-06, FR-07, FR-43)*
As a new user, I want to answer a few simple questions about my goals and experience so that the system proposes a plan that fits me.

- Exactly 7 questions, one per screen, each with visual options and a one-line description:
  1. Training goal: fat loss / muscle gain / general fitness.
  2. Experience: never trained / < 6 months / 6–12 months / returning after ≥ 6 months away.
  3. Training environment: commercial gym / home.
  4. Home equipment (shown only if home): multi-select: bodyweight only, dumbbells, resistance bands, bench, barbell, pull-up bar.
  5. Days per week: 2 / 3 / 4 / 5+.
  6. Session length: 30 / 45 / 60 / 90 min.
  7. Training restrictions: "Can you train without restrictions from injury or a medical condition?" Yes / No. Only the boolean is stored.
- After question 7, a separate **body data** step asks for body weight (required, 30–250 kg) and height (optional). Body weight is used only for starting-weight calculation (Appendix A1) and body-weight statistics; height is used only for display. This step is visually separated from the questionnaire and reminds the user of the consent given at registration.
- The user can go back and change an answer.
- Completable in ≤ 3 minutes (median, measured).
- If question 7 is answered "No": a disclaimer is shown stating that the plan is general training guidance, is not adapted to injuries or conditions, and that the user should consult a doctor or physiotherapist; the user must tap "I understand" to continue. The user is not blocked.
- On completion the system generates the first plan automatically.
- The questionnaire can be re-run at any time from account settings, pre-filled with previous answers; completing it triggers the new-plan offer from US-03.

**US-03: Profile editing** *(FR-04, FR-11)*
As a registered user, I want to edit my profile (including body weight and goal) so that recommendations stay relevant.

- All profile fields are editable from "My account".
- Changing the training goal **or** training environment triggers an offer to generate a new plan. If accepted, the current plan is archived (FR-11), never deleted; if declined, the current plan continues unchanged and the offer is not repeated for that change.
- Changing days per week or session length does not trigger a new plan; it takes effect on the next generation.
- Body-weight changes are stored as dated history for statistics.

**US-24: Login and password reset** *(FR-02, FR-03, NFR-15, NFR-17)* **(new)**
As a returning user, I want to log in quickly and recover my password if I forget it so that I never lose access to my data.

- Login with e-mail + password, Google or Facebook. The session persists across app restarts until explicit logout or 30 days of inactivity.
- After 5 consecutive failed attempts, the account is locked for 15 minutes; the user is told when they can retry. A successful login resets the failed-attempt counter.
- "Forgot password" sends a single-use reset link valid for 1 hour. The response is identical whether or not the e-mail exists (no account enumeration). At most 3 reset e-mails are sent per address per hour; requests over the limit get the same response without an e-mail. A user without a password (provider-only) can use the same flow to set one.
- The new password must satisfy NFR-15; all sessions are logged out after a successful reset, and the user then logs in with the new password.
- Every login attempt (success/failure, timestamp, IP, device type) is written to the security audit log, together with logout, sign out of all devices, completed password reset, provider linked, lockout triggered, and reuse of an already-used session-renewal token. Routine session renewals are not logged.
- "Sign out of all devices" in account settings ends every session, including the current one.
- A successful sign-in from a device the account has not used before triggers a new-device e-mail with the sign-in time, the device type, and advice to sign out of all devices and reset the password if it was not the user. No e-mail is sent for failed attempts or for the first sign-in right after registration.

**US-25: Health-data consent** *(NFR-22)* **(new)**
As a user who cares about my privacy, I want to understand and control how the app uses my body measurements so that I know exactly what I'm sharing.

- Consent is required at registration (US-01). The consent screen states in plain language what is collected (body weight, height, body measurements), why (starting-load calculation and body-weight statistics), that it is stored encrypted, and that it can be withdrawn.
- The user can view the consent (date, Privacy Policy version) in account settings at any time.
- Withdrawing consent is equivalent to closing the account: the user is warned that Liftingo cannot operate without body weight and is offered account deletion (US-18). No partial mode exists.
- Body weight is editable at any time; the previous value is kept as dated history.
- Grant events are logged with timestamp and Privacy Policy version.

### 5.2 Training plan and AI

**US-04: First plan** *(FR-07, NFR-03, NFR-27, NFR-28)*
As a beginner, I want a complete plan matched to my experience, availability and equipment so that I can start without composing a programme myself.

- Plan generated in < 10 s.
- Covers 4–12 weeks, 2–4 strength sessions/week. If the user selected 5+ days, the generator schedules at most 4 strength sessions and fills remaining days with optional cardio/mobility.
- Every strength exercise has sets, reps and a starting weight computed from the user's body weight and experience bracket (Appendix A, table A1); week 1 is a calibration week at 90 % of the computed value, after which the progression rules (US-07) take over.
- Includes recovery days and muscle-group rotation (validated by the methodology validator).
- Every strength session starts with a 5–10 min mobility warm-up block; cardio days are added only if the user's goal or answers call for it and are labelled "optional".
- The plan screen clearly states the plan is AI-generated and links to an explanation of what data was used.
- If the user declared training restrictions, the plan header additionally shows a persistent "Not adapted to injuries or medical conditions" note.
- The user can accept, request an alternative variant (max 3 per generation session; each variant is a full regeneration and counts toward the 5/24 h fair-use limit), edit, or cancel.

**US-05: Plan modification** *(FR-08)*
As a user with a generated plan, I want to add, remove and reorder exercises so that the plan fits my preferences and equipment.

- Editing available from the plan view.
- The system warns (does not block) when a change violates methodology rules (e.g. no recovery day) or adds an exercise requiring equipment outside the user's declared environment.
- Changes are saved on confirmation; the original can be restored within 24 h.

**US-06: Adaptation after a skipped workout** *(FR-09)*
As a user following a plan, I want suggestions on how to adjust it when I miss a session so that one missed day does not ruin the week.

- A skipped **strength** session is detected 24 h after its planned time; skipped optional cardio days do not trigger adaptation.
- The user is notified with three options: reschedule, redistribute volume, continue as planned.
- If no response within 24 h, the session is rescheduled to the next available day by default. This default is a deterministic rule, not an AI decision, and is disclosed in the notification.
- A session can be rescheduled at most once; if the rescheduled session is also skipped, it is marked as lost and the plan continues.
- After a break ≥ 14 days a shortened comeback plan is proposed.
- The choice is stored and not asked again for that session.

**US-07: Rule-based load suggestion** *(FR-10)* **(changed)**
As a user doing strength workouts, I want a suggested load for each exercise in my next session so that I know when and how much to increase without calculating it myself.

- The suggestion appears in the planned session view next to each exercise.
- It is computed from the last two or more logged performances of that exercise using the versioned rule set in Appendix A (no ML model).
- A one-sentence rationale is shown (e.g. "You completed all reps in the last two sessions, so the rule increases the load by 2.5 kg").
- The same inputs always produce the same suggestion.
- The user can reject the suggestion and enter their own load; rejection is logged for rule tuning.
- If fewer than two performances exist, the starting weight from the plan is shown with the note "not enough history yet".

**US-26: Viewing the plan** *(FR-07, FR-47)* **(new)**
As a user with an active plan, I want to see my week and today's session at a glance so that I know what to do without digging.

- Plan home shows: current week number of N, today's session (or "rest day"), and the remaining sessions of the week with status (planned / done / skipped / optional).
- Optional cardio/mobility days are visually distinct from strength sessions.
- Tapping a session shows its exercises with sets × reps, suggested weight (US-07) and rest time; tapping an exercise opens the library entry.
- A week switcher lets the user browse past and future weeks; past weeks are read-only.
- The "Generated by AI" label and the "How this plan was made" link are visible on the plan home.

**US-27: Manual plan without AI** *(FR-48, NFR-30)* **(new)**
As a user who doesn't want an AI plan, I want to build my own plan so that I can still use the log, progression suggestions and calendar.

- From plan settings the user can create an empty plan with a name, length (1–12 weeks) and sessions per week (1–6).
- Sessions and exercises are added with the same editor as US-05; the methodology validator warns but does not block.
- Manual plans are labelled "Built by you" and never carry the AI label.
- Progression suggestions, adaptation after skipped sessions and streaks work identically for manual plans.
- Only one plan can be active at a time; activating a manual plan archives the current one.

### 5.3 Workout log

**US-08: Fast set logging** *(FR-13, NFR-01, NFR-06, NFR-10)*
As a user at the gym, I want to log a set (reps + weight) quickly so that I spend time training, not typing.

- ≤ 3 taps per set, counted from the session screen with pre-filled values to a saved set (open exercise → adjust if needed → save); save completes in < 2 s.
- Reps and weight are adjusted with +/− steppers (step: 1 rep; 2.5 kg barbell, 1 kg dumbbell, 1 step machine/band); a keyboard is available on long-press but never required.
- The last set's values are pre-filled for the next set; for the first set of an exercise, the suggested load (US-07) is pre-filled.
- RPE is optional and hidden behind an "add RPE" expander so it never adds to the 3 taps.
- Key controls are reachable one-handed on a 6-inch screen.
- Works fully offline.

**US-23: Logging cardio and mobility** *(FR-45, FR-46)* **(new)**
As a user who also runs or stretches, I want to log cardio and mobility work in the same app so that my whole training week is in one place.

- A session can contain strength, cardio and mobility blocks in any combination.
- Cardio block = activity type + duration (required) + distance (optional) + RPE (optional). Saving takes ≤ 4 taps.
- Mobility block = exercise + duration or number of holds; sides (left/right) are not modelled, the user logs the total.
- Cardio and mobility blocks appear in the session summary, calendar and weekly statistics, but never affect 1RM, PRs or leaderboards.
- Works offline like strength logging.

**US-09: Rest timer** *(FR-15)*
As a user in an active session, I want the rest timer to start automatically after each set so that I keep a good pace.

- Starts automatically on set save; default duration configurable per exercise.
- Sound and vibration at the end. If the app is backgrounded and the OS prevents in-app audio/vibration, a system notification is sent at timer end (fallback for OQ-15).
- Can be paused, extended or shortened at any time.
- The timer never blocks logging: the user can save the next set while the countdown runs, which restarts the timer.

**US-10: Offline session** *(NFR-06, NFR-07)*
As a user training somewhere with poor signal, I want to log a whole workout offline so that I never lose data.

- All log functions work offline; an icon in the header shows offline status.
- Data syncs automatically when the connection returns.
- Conflicts are detected and resolved **per session**: the user sees both versions side by side (device / cloud, with timestamps) and chooses "keep this device", "keep cloud", or "merge" (union of sets, available only when no set number overlaps). Sessions without conflict sync silently.

**US-28: Session summary** *(FR-16)* **(new)**
As a user finishing a workout, I want a short summary so that I see what I achieved and can close the session with confidence.

- "End session" asks for confirmation if any planned exercise has zero logged sets.
- Summary shows: duration, total strength volume (kg), sets completed vs planned, cardio minutes/distance and mobility minutes if any, and any personal records set (with a PR badge animation).
- If the session fulfils a planned session, its status changes to "done" and the streak updates immediately.
- A session with no logged sets cannot be ended as "done"; it is discarded with confirmation.
- The summary is reachable later from the calendar (US-15).

**US-29: Ad-hoc session** *(FR-12, FR-20)* **(new)**
As a user who wants to train outside my plan today, I want to start a free session and add exercises as I go so that every workout is captured.

- "Start free session" is available from the home screen at any time, including on a rest day or while a planned session exists.
- Exercises are added via search (US-12) or from a "recent" list; each exercise opens the same set-logging UI as US-08.
- Ad-hoc sessions count toward statistics, PRs, streak and leaderboards, but never change the status of a planned session (a planned session skipped that day still triggers adaptation, US-06).
- Suggested loads (US-07) are shown for any exercise with history, regardless of plan.

**US-30: Editing and substituting** *(FR-14, FR-49)* **(new)**
As a user who made a logging mistake or found the equipment taken, I want to correct sets afterwards and swap an exercise mid-session so that my log stays accurate and my workout isn't blocked.

- Sets can be edited or deleted during the session and for **7 days** after the session ended; older sessions are read-only, with a message explaining why.
- Edits within the 7-day window recompute PRs, statistics, streak and leaderboard values; a PR that no longer holds is removed and the notification is not re-sent.
- During a session, "Swap exercise" on any planned exercise lists alternatives from the library with the same movement pattern (Appendix A1) that match the user's declared equipment first, then others with a warning.
- The swap applies to this session only; the plan is unchanged. Suggested load for the substitute comes from its own history or, absent that, its starting-weight ratio.
- Swapped exercises are marked in the session summary.

### 5.4 Exercise library

**US-11: Technique check** *(FR-19)*
As a beginner, I want to watch an animation and read the technique for each exercise so that I train correctly and reduce injury risk.

- Every exercise has a looping animation or, at minimum, a static illustration; the media type is recorded per entry and the thesis states the count of each.
- Technique text is split into start position, movement, common mistakes (each ≤ 3 short bullets), plus an optional beginner tip.
- The entry shows type, movement pattern, primary and secondary muscle groups, equipment and difficulty.
- Opens in one tap from the active session.

**US-12: Exercise search** *(FR-20)*
As a user building an ad-hoc session, I want to find an exercise by name quickly so that I don't scroll through the whole list.

- Results in < 0.5 s; partial matching ("squa" → "Barbell squat").
- Search matches Polish name, English name and aliases ("deadlift" and "martwy" both find *Martwy ciąg*), ignoring diacritics.
- Sorted by relevance, then by the user's usage frequency; favourites are pinned to the top when the query is empty.

### 5.5 Statistics

**US-13: Progress insight** *(FR-22, FR-24, FR-52)*
As a user active for a few weeks, I want charts of how I'm progressing so that I can judge objectively whether training works.

- Chart metrics depend on exercise type: weighted and band → max load per session, estimated 1RM (Epley) and volume; bodyweight and timed hold → best reps or seconds per session and current ladder step; cardio → weekly minutes and distance.
- Ranges: last week, month, 3 months, year, all time.
- Personal records are marked on the chart (both PR kinds, US-34).
- A short text summary is shown per chart (e.g. "Estimated 1RM up 12 kg in the last 3 months"). Summaries are computed from stored session data with database queries and rendered through fixed sentence templates: no AI, so no AI label is required.
- A body-weight chart (dated history from profile edits) is available in the statistics section with the same ranges.

**US-33: Muscle-group balance** *(FR-25)* **(new)**
As a beginner who doesn't know what "balanced" means, I want to see which muscle groups I've been working so that I notice if I'm neglecting something.

- Weekly view of hard sets per muscle group (primary muscles count 1, secondary 0.5), shown as a bar chart, for the last week and last 4 weeks.
- Groups with zero sets in the last 14 days are highlighted with a hint ("No back work in 2 weeks"), informational only.
- Computed from library metadata (Appendix B2), so custom exercises contribute via their declared primary muscle group.

**US-34: Personal records** *(FR-24)* **(new)**
As a user chasing progress, I want the app to recognise my records so that I don't have to keep track myself.

- Two PR kinds are tracked per weighted/band exercise: **heaviest set** (max weight at ≥ 1 rep) and **best estimated 1RM** (Epley: weight × (1 + reps/30), reps ≤ 12 only). Either can trigger a PR.
- Bodyweight and timed-hold exercises track best reps / longest hold at the current ladder step.
- Badges and the session summary use the heaviest-set PR; personal goals (US-17) and progress charts use estimated 1RM.
- A PR is recorded at set save (works offline) and confirmed on the session summary; edits within the 7-day window (US-30) may revoke it.
- The PR list per exercise shows date, set and which kind of record it was.

**US-14: Data export** *(FR-27, FR-28, NFR-20)* **(changed)**
As a user who wants to own my data or show it to a trainer, I want to export my training history so that someone outside the app can review it.

- Export is available from account settings, for a chosen period (last month / 3 months / all) and produces a file in < 30 s.
- **CSV** (machine-readable): one row per set or block with columns: date, session id, session type (planned/free), exercise name PL, exercise name EN, exercise type, set number, reps, weight, band level, seconds, distance, duration, RPE, is_PR, swapped_from, session note. Cardio and mobility rows leave strength columns empty.
- **PDF** (trainer-readable, fixed layout): (1) cover with name, period, body weight at start/end; (2) weekly overview (sessions done vs planned, strength volume, cardio minutes); (3) per-exercise table for the 10 most-trained exercises (best set, estimated 1RM at start/end, progression chart); (4) all PRs in the period; (5) muscle-group balance chart; (6) session log listing every session with exercises and sets, notes included; (7) footer stating the plan was AI-generated where applicable (NFR-28).
- The user can toggle "include notes" and "include body weight" before generating.
- Exported files are generated on demand and not retained server-side beyond the download.

### 5.6 Calendar, gamification and goals

**US-15: Calendar view** *(FR-26)*
As a user planning workouts, I want to see planned and completed workouts in a calendar so that I feel in control of my schedule.

- Monthly and weekly views.
- Completed, planned, skipped, lost and optional (cardio/mobility) sessions are distinguishable by colour and icon.
- Tapping a day opens the session summary (done) or the planned session view (upcoming).
- A planned session can be manually moved to another day of the same week via "Move to…" or drag-and-drop; one manual move per session (shared with the adaptation reschedule limit, US-06). Moving to a day that already has a strength session is blocked with a message.

**US-16: Badges** *(FR-29)*
As a user who loses motivation easily, I want to earn badges for consistency and milestones so that I get extra satisfaction from training.

- The 15 launch badges are defined in Appendix C with testable unlock conditions; awarded automatically when the condition is met (checked at session end and on friend/leaderboard events).
- Notification with a short description on unlock; a badge is awarded once, never revoked (even if the underlying PR is later edited away).
- Badge gallery in the profile shows earned and locked badges; locked badges show their condition.

**US-17: Personal goal** *(FR-31)* **(changed: no goal limit)**
As an ambitious user, I want to set a concrete goal (e.g. barbell squat 100 kg) and track it so that I have a clear target.

- Goal = exercise + target value + deadline. Target is a load (weighted/band, compared against estimated 1RM), reps (bodyweight) or seconds (timed hold). Only exercise-based goals are supported in v1.
- Progress bar = (current best − value at goal creation) / (target − value at creation), floored at 0.
- On achievement: congratulatory notification and the "Goal reached" badge (Appendix C).
- When the deadline passes unmet, the goal moves to "expired" and the user is offered to extend the deadline (any future date) or archive it; extending keeps progress.
- No limit on the number of active goals.

**US-35: Training streak** *(FR-30)* **(new)**
As a beginner who needs consistency more than intensity, I want a streak that rewards showing up every week so that a rest day never feels like failure.

- Streak unit is the **calendar week** (Mon–Sun, user's timezone). A week counts if the user completed at least the number of strength sessions planned for that week, or at least 2 sessions of any type if no plan is active. Cardio and mobility sessions count toward the "any type" rule.
- The streak increments when a week is completed and resets to 0 at the end of a week that did not meet the target.
- The home screen shows the current streak and the sessions still needed this week ("1 more session to keep your streak").
- Longest streak is stored and shown in the profile.
- Weeks with an accepted comeback plan (US-06) use the comeback plan's target.

### 5.7 Social (new)

**US-19: Adding friends** *(FR-32 – FR-35, NFR-34)*
As a user who trains with friends, I want to connect with them in the app so that we can motivate each other.

- Social features are disabled until the user turns on a social profile and picks a username (3–20 chars, letters/digits/underscore, unique, case-insensitive). **Usernames cannot be changed** in v1; the user is told this before confirming.
- On first enabling the social profile, the empty friends screen shows an "Invite friends" call to action with the invite link and a share button.
- A user can be found by exact username or by e-mail (e-mail lookup works only if the target opted in to e-mail discovery), or via an invite link. Invite links are **multi-use, valid 7 days, max 10 acceptances**, and revocable by the owner from settings; opening an expired or revoked link shows a message and nothing else.
- Any of the three paths creates a **friend request**; the recipient must explicitly accept it before the two users are friends. Until then no data is visible in either direction.
- Pending requests can be cancelled by the sender or declined by the recipient; declines are not shown to the requester and a repeat request is allowed after 30 days.
- A friend can be removed at any time; removal is silent and immediate on both sides.
- A user can block another user; blocking removes any friendship, prevents future requests and hides the blocker from the blocked user's leaderboards and search. The blocked list is managed in settings.

**US-20: Friend leaderboard** *(FR-36 – FR-38, FR-40, NFR-35)*
As a user with friends in the app, I want to see how I rank against them this week so that I stay motivated to keep up.

- Leaderboards are visible only if the user has ≥ 1 accepted friend and has opted in (FR-38).
- Three tabs: **strength workouts** completed this week, **strength volume** (kg) this week, **current streak** (weeks, as defined in US-35; any session type counts toward the streak).
- Period: Monday 00:00 – Sunday 23:59 in the viewer's timezone; the last 4 completed weeks are browsable.
- Every opted-in friend appears, including those with 0 this week; ties share the same rank.
- Each entry shows username, avatar (optional) and the metric value, nothing else. A metric the friend has hidden shows "—" and that friend is ranked last for that tab.
- Tapping a friend opens their friend profile (US-36).
- Overtaken notification (opt-in): sent when a friend passes the user on a metric, at most **one per metric per day**.

**US-21: Controlling what friends see** *(FR-39, FR-40, NFR-34, NFR-36)*
As a privacy-conscious user, I want to control exactly what my friends can see so that I can use social features without exposing personal data.

- Privacy settings list every shareable item (each leaderboard metric, badges earned, activity status, e-mail discovery) with an individual toggle, all off by default.
- A "Preview as friend" button renders the user's friend profile and leaderboard rows exactly as a friend would see them with the current settings.
- Body weight, height, age, sex, goal, notes, and individual sets are never shareable.
- Turning off the social profile removes the user from all friends' leaderboards within 5 minutes and hides all data; friendships are kept for reactivation.
- Requesting account deletion removes the user from all friend lists and leaderboards immediately (before the grace period ends).

**US-36: Friend profile** *(FR-53)* **(new)**
As a user, I want to see a friend's profile so that I can cheer on their progress without seeing anything private.

- The friend profile shows only: username, avatar, badges earned (if the friend shares badges) and current streak (if shared). Hidden items show "—".
- Nothing else is shown or reachable: no body data, sessions, exercises, loads, notes or goals (NFR-36).
- Actions available: remove friend, block.

### 5.8 Privacy and compliance

**US-18: Account deletion** *(FR-05, NFR-19, NFR-38)*
As a user leaving the app, I want to permanently delete my account and data so that I keep control of my personal data under the right to erasure.

- Available from the Privacy centre (US-37); requires confirmation and password re-entry (or re-authentication with the linked external provider).
- On confirmation the account enters a **7-day grace period**: it is deactivated (login shows "Your account is scheduled for deletion on <date>. Restore?"), removed from all friends' lists and leaderboards immediately, and excluded from all processing. Logging in and choosing "restore" cancels the deletion and restores social links.
- After 7 days, all personal data (profile, body data, sessions, plans, custom exercises, badges, goals, consents, friend links, invite links, security audit log, known devices) is **deleted immediately and irreversibly** from the live database; historical leaderboard rows of friends are anonymised (NFR-38).
- Backups containing the user's data are purged within 30 days of deletion.
- A confirmation e-mail is sent when the deletion executes; it is the last message the user receives.

**US-37: Privacy centre and personal-data export** *(NFR-20, NFR-21, NFR-22)* **(new)**
As a user, I want one place to see what the app knows about me and to take my data with me so that I can exercise my GDPR rights without contacting support.

- The Privacy centre (in account settings) shows: each consent (Terms & Privacy, health data, marketing, social sharing toggles) with its status, date and Privacy Policy version; links to the Privacy Policy and Terms; the "Export my data" and "Delete my account" actions.
- "Export my data" produces a **JSON archive** containing every personal-data record: profile and body-weight history, questionnaire answers, consents, all plans (with AI-generation metadata), all sessions and sets, custom exercises, PRs, goals, badges, streak history, friends list (usernames only), blocked list, invite links, notification settings, security audit log, known devices.
- The archive is generated within 5 minutes; the user is notified in-app and by e-mail with a download link valid **24 h**, after which the file is deleted server-side.
- Export is available at most once per 24 h.
- The JSON schema is documented in the technical documentation so the file is machine-readable (Art. 20 GDPR).

**US-22: Understanding the AI and plan health** *(FR-57, NFR-27 – NFR-31)* **(new)**
As a user receiving an AI-generated plan, I want to know that it was generated by AI, what it was based on, and that it passed independent safety checks so that I can decide whether to trust it.

- Every AI-generated plan carries a visible "Generated by AI" label; manual plans carry "Built by you".
- A **"How this plan was made"** screen, reachable from the plan home, has three parts:
  1. **Inputs used**: goal, experience, environment and equipment, days/week, session length, body weight, in plain language.
  2. **Plan health**: one line per methodology-validator rule, each with pass/fail and a template sentence (e.g. "✓ Recovery: no muscle group is trained on consecutive days"; "✗ Recovery: legs are trained on Monday and Tuesday"). The rule list is the validator's six locked rules (OQ-17); the screen renders whatever rules the validator exposes, so adding a rule later requires no UI change.
  3. **Generation record**: generation date, plan-generator version, rule-set version. The number of generation attempts is logged (NFR-31) but **not shown**.
- Plan health is **re-evaluated after every manual edit** (US-05) and after adaptation (US-06); a failing rule shows the same warning text the editor shows, and a small status indicator on the plan home reflects the current state (all pass / warnings).
- Manual plans show parts 2 and 3 only (no AI inputs; record shows "Built by you" and the rule-set version).
- The screen states that the plan is training guidance, not medical advice, and links to a short explanation of how the generator works.
- The user can always reject the AI plan and build a plan manually (US-27).
- Rule-based progression suggestions are labelled "Based on training rules" and are visually distinct from AI-generated content.

## 6. Functional Requirements

Priority (thesis scope): **P0** = thesis deliverable, the project is not complete without it; **P1** = implemented if time allows, in the order listed in section 10; **P2** = designed for but not built. Acceptance criteria are in the linked user stories unless listed here.

Priority review of 6 Sep 2026: FR-15, FR-23, FR-26, FR-27, FR-45, FR-53 moved P0 → P1; FR-11, FR-30 moved P1 → P0 (dependencies of P0 features). FR-09 kept at P0 as a headline differentiator.

### 6.1 Account management

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-01 | Registration | Create an account with e-mail + password or via Google or Facebook. Requires confirmation of age ≥ 18 and acceptance of Terms and Privacy Policy; required explicit health-data consent (body weight needed for starting loads); optional marketing consent. E-mail verification via single-use link (24 h), which doubles as the welcome e-mail; e-mails supplied by an external provider are trusted as verified, skip verification and get a welcome-only e-mail; such an e-mail links to an existing account automatically, with pre-hijacking protection for unverified accounts. At most 3 verification e-mails per address per hour. Unverified accounts deleted after 7 days. | P0 |
| FR-02 | Login | Login with credentials or an external provider. Session persists 30 days; account locked for 15 min after 5 consecutive failed attempts. New-device e-mail on the first successful sign-in from an unrecognised device. Logout of the current device or of all devices. | P0 |
| FR-03 | Password recovery | Reset via single-use link valid 1 h; no account enumeration; at most 3 reset e-mails per address per hour; all sessions invalidated on reset. | P0 |
| FR-04 | Profile editing | Edit first name, age, sex, body weight, height, goal, experience, environment, equipment, days/week, session length. Goal or environment change offers a new plan and archives the old one. | P0 |
| FR-05 | Account deletion | Request deletion; 7-day grace period with restore; then immediate irreversible deletion from live data, backup purge ≤ 30 days. | P0 |
| FR-55 | Privacy centre **(new)** | Single screen with consent history (status, date, policy version), policy links, data export and account deletion. | P0 |
| FR-56 | Personal-data export **(new)** | Complete JSON archive of all personal data, ready ≤ 5 min, link valid 24 h, once per 24 h. | P0 |

### 6.2 Training plans and AI

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-06 | Onboarding questionnaire | Exactly 7 questions (goal, experience, environment, home equipment, days/week, session length, training restrictions) followed by a body-data step (body weight required, height optional) after first login; answers stored in the profile; re-runnable from settings. | P0 |
| FR-07 | AI plan generation | Generate a 4–12-week plan with 2–4 strength sessions/week from profile + questionnaire, fully matched to the user's environment (gym or home). Starting weights per Appendix A. The generator selects only exercises with difficulty ≤ 2, uses the mobility type for warm-ups and the cardio type for optional cardio days, and never selects custom exercises. Includes a mobility warm-up in every strength session and optional cardio days per goal (fat-loss 2/week, general fitness 1/week, muscle gain 0/week, OQ-16). Fair-use limit: max 5 generations per user per 24 h. Every plan passes the methodology validator before display. | P0 |
| FR-57 | Plan transparency screen **(new)** | "How this plan was made": inputs used, plan health (validator rules with pass/fail, re-evaluated after every edit and adaptation), generation record without attempt count. Renders validator rules dynamically. | P0 |
| FR-47 | Plan view **(new)** | Plan home with current week, today's session, week status and week switcher; AI label visible. | P0 |
| FR-48 | Manual plan **(new)** | Create and activate a user-built plan (1–12 weeks, 1–6 sessions/week) using the plan editor; validator warns only; no AI label. | P1 |
| FR-08 | Plan modification | Add, remove, reorder exercises and sessions; warn on methodology violations and update plan health (FR-57); restore original within 24 h. | P0 |
| FR-09 | Adaptation after skipped session | Detect skipped strength session after 24 h; offer reschedule / redistribute / continue; default reschedule after 24 h without response; max one reschedule per session; comeback plan after ≥ 14 days. | P0 |
| FR-10 | Rule-based progression suggestion **(changed)** | After each session, compute a suggested load per exercise using a versioned expert-rules engine (no ML). Show rationale. Deterministic. Rule set stored with a version number; every suggestion records the rule version used. | P0 |
| FR-11 | Plan archive | History of all plans the user has followed. | P0 |
| FR-43 | Training-restriction declaration **(new)** | Single yes/no question in onboarding and editable in profile. Stores only the boolean. "No" triggers a mandatory acknowledged disclaimer and a persistent note on generated plans; it never blocks the user and never changes plan content. | P0 |
| FR-44 | Injury-aware plan generation **(new, future)** | Capture injured body area and exclude/substitute exercises. Requires Art. 9 consent, domain-expert exclusion rules and updated AI Act assessment. | P2 |
| FR-41 | ML-based progression **(new, future)** | Replace or augment FR-10 with a learned model once data volume, validation methodology and AI Act classification are settled. Design FR-10 so the rules engine and a future model share the same input/output interface. | P2 |

### 6.3 Workout log

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-12 | Start session | Start a session from the active plan or a free (ad-hoc) session; ad-hoc sessions count toward stats/streak/leaderboards but do not change planned-session status. | P0 |
| FR-13 | Log set | Save a set in ≤ 3 taps from the session screen using steppers and pre-filled values; RPE optional behind an expander. Manually entered loads > 2× the exercise's Appendix A1 starting estimate trigger a non-blocking confirmation (OQ-12). | P0 |
| FR-14 | Edit set | Edit or delete a set during the session and up to 7 days after it ended; derived stats recomputed. | P0 |
| FR-15 | Rest timer | Auto-start after set save; duration per exercise; pause/extend/shorten. | P1 |
| FR-16 | End session | Manual end with confirmation; summary of duration, volume, sets done vs planned, cardio/mobility totals, PRs; empty sessions discarded. | P0 |
| FR-17 | Session note | Free-text note per session. | P1 |
| FR-49 | Exercise substitution **(new)** | Swap a planned exercise mid-session for a same-pattern alternative from the library; session-only; equipment-matching alternatives listed first. | P1 |
| FR-45 | Cardio block **(new)** | Log a cardio block (activity type, duration, optional distance, optional RPE) inside any session. ≤ 4 taps. Works offline. | P1 |
| FR-46 | Mobility block **(new)** | Log a mobility block (exercise, duration or holds) inside any session. Works offline. | P0 |

### 6.4 Exercise library

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-18 | Browse library | Catalogue filterable by type, movement pattern, muscle group, equipment, environment (gym/home), difficulty and favourites. | P0 |
| FR-19 | Exercise details | Every entry implements the data model in Appendix B (identity, classification, equipment, progression data, content). Animation or static illustration per entry. | P0 |
| FR-20 | Search | Partial, diacritic-insensitive search over Polish name, English name and aliases; usage-weighted ranking; favourites pinned. | P0 |
| FR-21 | Custom exercises **(changed)** | User-defined exercises that must pick a type, movement pattern, equipment and (for weighted) an A1 starting-weight row and A2 class; private; fully supported by log, rules and PRs; never chosen by the generator. | P1 |
| FR-50 | Favourites **(new)** | Mark/unmark exercises as favourites; pinned in search and available as a filter. | P1 |
| FR-51 | Starter library **(new)** | 45–55 fully specified exercises covering all six types and both environments so the generator can build a complete gym plan and a complete home plan (target split: ~25 weighted, ~10 bodyweight, ~5 band, ~4 timed hold, ~5 cardio, ~6 mobility). | P0 |

### 6.5 Statistics

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-22 | Progress charts | Per-exercise charts with type-specific metrics (load/e1RM/volume; reps or seconds + ladder step; cardio minutes/distance) over a selectable range; template-based text summary. | P0 |
| FR-52 | Body-weight chart **(new)** | Body-weight history chart with the same ranges. | P1 |
| FR-23 | Aggregate statistics | Workout count, total time, total volume, frequency; weekly cardio minutes and distance; weekly mobility minutes. | P1 |
| FR-24 | Personal records | Two PR kinds per weighted exercise (heaviest set, best Epley e1RM); best reps/hold for bodyweight/timed; recorded at set save, revocable by edits. | P0 |
| FR-25 | Muscle-group balance | Weekly hard sets per muscle group (primary 1, secondary 0.5) with neglected-group hint. | P1 |

### 6.6 Calendar, export, gamification

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-26 | Calendar | Monthly and weekly views of planned, completed, skipped, lost and optional sessions; manual move of a planned session within its week (once). | P1 |
| FR-27 | CSV export | Per-set/block CSV for a chosen period with the column set defined in US-14. | P1 |
| FR-28 | PDF report | Trainer-readable PDF with the fixed layout defined in US-14 (overview, per-exercise progress, PRs, muscle balance, session log). | P1 |
| FR-29 | Badges | The 15 badges in Appendix C, awarded automatically and permanently. | P1 |
| FR-30 | Streak | Week-based streak: consecutive calendar weeks meeting the planned strength-session count (or ≥ 2 sessions of any type without a plan). Longest streak stored. | P0 |
| FR-31 | Personal goals | Exercise-based goals (load vs e1RM, reps, or seconds) with deadline, progress bar, expiry with extend/archive. No limit. | P1 |

### 6.7 Social (new)

| ID | Name | Description | Pri. |
|---|---|---|---|
| FR-32 | Social profile | Opt-in social profile with unique, immutable username (3–20 chars, letters/digits/underscore, case-insensitive) and optional avatar. Off by default; first-run state promotes the invite link. | P0 |
| FR-33 | Find users | Find by exact username, by e-mail (only if target opted in to e-mail discovery), or by invite link (multi-use, 7-day expiry, max 10 acceptances, revocable). All paths create a request that the recipient must accept. | P0 |
| FR-34 | Friend requests | Send, accept, decline, cancel. Max 200 friends per user. Declined requests are not revealed to the sender; a new request to the same user is allowed after 30 days. | P0 |
| FR-35 | Remove and block | Remove a friend (silent); block a user (prevents requests and mutual visibility). Blocked list manageable in settings. | P0 |
| FR-36 | Weekly friend leaderboard | Rank the user and all opted-in friends (including those at 0) by strength workouts, strength volume, or current streak for the current week in the viewer's timezone. Ties share rank; hidden metrics rank last. | P0 |
| FR-37 | Leaderboard history | Show the last 4 completed weeks. | P1 |
| FR-38 | Leaderboard opt-in | The user must opt in to appear on friends' leaderboards; opt-out takes effect within 5 minutes. | P0 |
| FR-39 | Per-metric sharing controls | Individual toggle for each leaderboard metric, badges and activity status; all off by default. Non-shareable data is enumerated in NFR-36. | P0 |
| FR-40 | Social notifications | Notify on friend request received, request accepted, and (opt-in) overtaken on a leaderboard, capped at one per metric per day. Each type can be disabled. | P1 |
| FR-42 | Report user | Report a username or avatar as inappropriate to an admin queue. Requires a moderation backend; deferred. | P2 |
| FR-53 | Friend profile **(new)** | Read-only view of a friend: username, avatar, shared badges, shared streak; remove/block actions. | P1 |
| FR-54 | Preview as friend **(new)** | Render the user's own friend profile and leaderboard rows as friends see them under current privacy settings. | P1 |

---

## 7. Non-Functional Requirements

### 7.1 Performance

| ID | Description | Pri. |
|---|---|---|
| NFR-01 | Saving a set completes in ≤ 2 s under normal load (p95). | P0 |
| NFR-02 | First load on mobile over stable 4G ≤ 3 s (p75). | P0 |
| NFR-03 | AI plan generation ≤ 10 s (p95); technical retries up to 3. | P0 |
| NFR-37 | Leaderboard view loads in ≤ 1.5 s (p95) for a user with 200 friends. **(new)** | P1 |

### 7.2 Reliability

| ID | Description | Pri. |
|---|---|---|
| NFR-06 | Full workout logging offline; sync on reconnect. | P0 |
| NFR-07 | Sync conflict handling across devices, resolved per session (keep device / keep cloud / merge non-overlapping sets). | P0 |
| NFR-08 | Backups with restore point ≤ 24 h old. | P0 |

### 7.3 Usability and accessibility

| ID | Description | Pri. |
|---|---|---|
| NFR-09 | Mobile-first, fully responsive. | P0 |
| NFR-10 | Core log functions usable one-handed. | P0 |
| NFR-11 | New user can start the first workout ≤ 5 min after registration. | P0 |
| NFR-12 | WCAG 2.1 AA. | P1 |

### 7.4 Security

| ID | Description | Pri. |
|---|---|---|
| NFR-13 | TLS 1.2+ for all client–server traffic. | P0 |
| NFR-14 | Sensitive data (personal, biometric) encrypted at rest. | P0 |
| NFR-15 | Password policy: ≥ 8 chars, ≥ 1 digit, ≥ 1 special character. | P0 |
| NFR-16 | Optional TOTP 2FA. | P2 |
| NFR-17 | Security audit log with retention policy: every login attempt plus logout, sign out of all devices, completed password reset, provider linked, lockout triggered, and session-renewal token reuse. | P0 |

### 7.5 Legal and compliance: GDPR (RODO) and EU AI Act

**GDPR**

| ID | Description | Pri. |
|---|---|---|
| NFR-18 | Full GDPR compliance for all personal-data processing; a Records of Processing Activities (RoPA) entry and a full **DPIA** (required per OQ-2) are written as part of the thesis, covering health-category data and the social module. | P0 |
| NFR-19 | Right to erasure: deletion executes 7 days after request (grace period), removes all live personal data immediately at that point; backups purged within 30 days. | P0 |
| NFR-20 | Data portability: complete JSON archive of all personal data with a documented schema (FR-56). | P0 |
| NFR-21 | Privacy Policy and Terms shown at registration; acceptance is explicit, logged with timestamp and document version. Policy versioning is stored, but a re-acceptance flow for policy updates is out of scope for v1 (no updates planned). | P0 |
| NFR-22 | Health-category data (body weight, height, measurements) requires separate, explicit consent (Art. 9(2)(a) GDPR) collected at registration. The consent is a condition of the service because body weight is necessary to compute safe starting loads (manual entry was rejected on safety grounds, US-04); this necessity is documented in the DPIA (OQ-2, resolved). Withdrawal leads to account deletion (US-18). | P0 |
| NFR-34 | Social features process data on an opt-in basis under **consent (Art. 6(1)(a), OQ-3)**; the legal basis and the exact data shared are stated in the Privacy Policy and shown in-app when the social profile is enabled. | P0 |
| NFR-35 | Leaderboards expose only aggregated metrics chosen by the user; no raw session data, health data or profile fields. **(new)** | P0 |
| NFR-36 | Non-shareable data (never visible to other users): e-mail, age, sex, body weight, height, training goal, session notes, individual sets and loads, personal goals. **(new)** | P0 |
| NFR-38 | Data of a deleted user is removed from other users' leaderboard history and friend lists immediately; historical leaderboard rows are anonymised. **(new)** | P0 |

**EU AI Act** (Regulation (EU) 2024/1689): applies to the AI plan generator; the rule-based progression engine is not an AI system under the Act's definition, which is one reason for change #3.

| ID | Description | Pri. |
|---|---|---|
| NFR-27 | **Risk classification**: the AI plan generator is classified as **limited-risk under Art. 50** (transparency obligations only), per the reasoning resolved in OQ-1. Documented as a section of the thesis in lieu of formal legal sign-off. | P0 |
| NFR-28 | **Transparency to users**: every AI-generated plan is labelled as AI-generated at the point of display and in exports; the label cannot be dismissed permanently. | P0 |
| NFR-29 | **Explainability**: the user can view which inputs were used to generate the plan and a plain-language description of how the generator works; the plan is presented as training guidance, not medical advice. Body weight is an input (starting-load calculation only); injury and medical-condition data are never inputs to the generator. | P0 |
| NFR-30 | **Human control**: the user can reject, edit, or replace any AI-generated plan; no AI output is applied without explicit user acceptance. Plan adaptation proposals (FR-09) also require acceptance, except the documented 24 h default reschedule. | P0 |
| NFR-31 | **Technical documentation and logging**: model/prompt version, input summary and validator result are logged for every generation and retained for 12 months; the generator has a maintained model card / system description. | P0 |
| NFR-32 | **Safety guardrails**: the methodology validator is a mandatory, deterministic gate on **generator output**; plans that fail validation after 3 attempts are never shown. The generator must not produce starting loads above the ratios in Appendix A1 for the user's declared experience bracket. Manually entered loads are governed separately by the sanity check in OQ-12/FR-13. The validator implements the six rules locked in OQ-17 (muscle rotation, recovery days, weekly volume, starting-load ceiling, equipment match, difficulty ≤ 2) and exposes its rule list and per-rule results so FR-57 can display them. | P0 |
| NFR-33 | **AI literacy**: onboarding includes a short screen explaining what in the app is AI-generated and what is rule-based. The thesis includes a dedicated section summarising the applicable AI Act obligations and how each is met, in lieu of staff training. | P1 |

### 7.6 AI endpoint protection and data retention

| ID | Description | Pri. |
|---|---|---|
| NFR-39 | **AI generation limits**: (a) fair-use quota 5 generations per verified user per 24 h, 1 per 24 h for unverified accounts (variants count); (b) **one in-flight generation per user**: a second request while one is running returns the status of the running one; (c) **global ceiling of 300 generations per calendar day** across the application, above which generation is refused with "try again tomorrow" and an alert is raised; (d) hard client-side timeout of 15 s per generator call, after which the call counts as a technical failure and is retried per FR-07 (max 3). All limits are configuration values, not code constants. | P0 |
| NFR-40 | **Retention schedule** (see table below). Application and error logs must never contain personal data (no e-mail, name, body data or set values). Retention jobs run daily and are logged. | P0 |

Retention schedule (NFR-40):

| Data | Retention | Rationale |
|---|---|---|
| AI generation records (NFR-31) | 12 months, then deleted; user id replaced by an anonymous token when the account is deleted | AI Act traceability without retaining personal data |
| Security audit log (NFR-17) | 90 days | Security investigation window |
| Application / error logs | 30 days | Debugging only; personal-data-free |
| Notification records | 30 days after read | No further use |
| Leaderboard weekly snapshots | 4 completed weeks (FR-37), older rows deleted | Only 4 weeks are ever shown |
| Product analytics / verification events | Anonymised (user id removed) 30 days after collection | Needed for the thesis metrics, not for profiling |
| Exported files (JSON, PDF, CSV) | 24 h | Stated in US-14 / US-37 |
| Backups | Rolling 30 days; deleted-account data purged within that window | NFR-19 |
| Grace-period (deactivated) accounts | 7 days | US-18 |
| Unverified accounts | 7 days | US-01 |
| Known devices (FR-02) | 12 months after the last sign-in from the device | Needed only to recognise returning devices |
| E-mail link tokens (verification, password reset, provider link) | 24 h after the link was used, expired or replaced | No further use once the link cannot be used |

### 7.6b Notifications **(new)**

| ID | Description | Pri. |
|---|---|---|
| NFR-41 | Notifications are **in-app plus e-mail** for events that matter if missed (friend request received/accepted, sign-in from a new device, deletion confirmation and grace-period reminder, export ready, plan generation failed); all other events (rest timer end, overtaken, badge, streak) are **in-app only**. No push notifications in v1, avoiding iOS PWA push limitations entirely (independent of the OQ-15 timer question). | P0 |

### 7.7 Compatibility and maintenance

| ID | Description | Pri. |
|---|---|---|
| NFR-23 | Current and two previous major versions of Chrome, Safari, Firefox, Edge. | P0 |
| NFR-24 | Installable PWA; this is the only delivery platform for v1 (see Non-Goals). | P0 |
| NFR-25 | UI in **Polish only** in v1. All UI strings are externalised (i18n scaffolding) so a second language is a translation task, not a code change; exercise names are stored bilingually (Appendix B2) regardless. | P0 |
| NFR-26 | Real-time technical and business monitoring. | P1 |

---

## 8. Verification Criteria (thesis)

Replaces market success metrics. Each criterion states how it will be demonstrated.

| Goal | Criterion | Method | Pass threshold |
|---|---|---|---|
| G1 | Time from registration to active plan | Usability test, ≥ 5 testers, median | ≤ 5 min |
| G2 | Taps to save a set | UI walkthrough count | ≤ 3 |
| G2 | Set save latency (online and offline) | Automated performance test, p95 | ≤ 2 s |
| G3 | Plan generation latency | Performance test, p95 | ≤ 10 s |
| G3 | Suggestion coverage | Automated test over seeded histories | 100 % of strength exercises with ≥ 2 performances |
| G3 | Rule engine determinism | Unit test: same input → same output, rule version logged | 100 % |
| G4 | Environment parity | Checklist: every P0 flow tested with a home-only and a gym profile | No gym-only flow |
| G5 | Social end-to-end | Integration test: connect, opt in, leaderboard visible, hidden metric shows "—", opt-out removes within 5 min | Pass |
| G6 | Functional completeness | Acceptance-criteria checklist per P0 requirement | 100 % P0 |
| G6 | Offline sync and conflict resolution | Test: two devices, conflicting edits | User prompted, no data loss |
| G7 | GDPR / AI Act self-assessment | Checklist mapped to NFR-18 – NFR-38 | 0 open items |
| — | Removed criteria | NFR-04 (concurrency) and NFR-05 (uptime) dropped 6 Sep 2026 as untestable in thesis scope; no load test replaces them | — |
| G8 | AI / rule boundary traceability | Log inspection: every plan has model version + validator result; every suggestion has rule version | 100 % |
| — | Usability | System Usability Scale after test session | SUS ≥ 70 |
| — | Accessibility | Automated audit (e.g. Lighthouse/axe) | No critical WCAG 2.1 AA violations |

## 9. Open Questions

Blocking questions must be answered before implementation of the affected requirement starts. Ordered by requirement, not by ID (IDs are historical and kept stable for traceability).

**Blocking**

| ID | Question | Owner | Blocks |
|---|---|---|---|
| OQ-5 | Exact numeric values in Appendix A (ratios, rep ranges, deload %, increments) still need domain-expert review; a methodology note grounding the general approach has been added to Appendix A so the draft is not arbitrary, but specific numbers are not yet validated. | Domain expert / thesis author | FR-10 |
| OQ-15 | PWA on iOS: can background rest timers, vibration and audio alerts be delivered reliably? If not, the fallback is a foreground-only timer plus a system notification at timer end (already drafted in US-09). | Engineering | FR-15 |

**Non-blocking**

| ID | Question | Owner |
|---|---|---|

**Resolved during this review**

| ID | Resolution |
|---|---|
| OQ-1 | Classified as **limited-risk (Art. 50, transparency obligations)**: the generator prescribes exercises for healthy adults only, never diagnoses or treats a condition, injury-aware decisions are explicitly out of scope (FR-44, P2), and NFR-32 bars it from exceeding the user's experience bracket: none of the Annex III categories (e.g. medical devices, employment, credit, law enforcement) apply. Reasoning recorded here for the thesis. |
| OQ-2 | A DPIA **is required** (large-scale processing of Art. 9 special-category data from every user) and will be written as a full thesis document, not just asserted. It must justify consent-as-condition-of-service (Art. 7(4)) for body weight: the alternative, a manually entered starting weight, was considered and rejected in US-04 on safety grounds (self-selected loads risk injury for beginners), which supports functional necessity. |
| OQ-3 | Legal basis for social/friend data: **consent (Art. 6(1)(a))**. Every social feature is optional, off by default, and revocable without losing core app functionality (US-21 removes the user from leaderboards within 5 minutes), which distinguishes it from the necessity-based basis used for body weight. |
| OQ-12 | Manually entered loads (US-07 override, US-08 set entry) are **flagged, not blocked**: if a value exceeds **2× the generator's Appendix A1 starting-weight estimate** for that user and exercise, the app shows "This looks unusually high, please confirm" and logs the override; the user may still proceed. This is a data-sanity guard, not a training-methodology ceiling (NFR-32 remains about generator output only). |
| OQ-16 | Optional cardio days by goal: **fat-loss → 2 days/week, general fitness → 1 day/week, muscle gain → 0 days/week** (cardio can still be logged ad-hoc, US-29, just not scheduled by the generator). |
| OQ-17 | Methodology validator locked at **six rules**: (1) muscle-group rotation, (2) recovery days between sessions for the same muscle group, (3) weekly volume within the beginner range (Appendix A2-derived), (4) starting-load ceiling per Appendix A1, (5) equipment match to the user's declared environment, (6) exercise difficulty ≤ 2. Each renders as a pass/fail line on the FR-57 transparency screen. |
| OQ-4 | Fair-use and abuse limits: NFR-39 (5/day verified, 1/day unverified, one in-flight, global 300/day ceiling as a tunable placeholder, 15 s timeout). |
| OQ-6 | Invite links: multi-use, 7-day expiry, max 10 acceptances, revocable (FR-33). |
| OQ-8 | Streak definition: week-based, any session type counts toward the "any type" rule (US-35). |
| OQ-14 | Injury/health exclusion: warn-and-proceed with a single yes/no restriction question; no condition data collected (FR-43). Health-data consent made mandatory at registration instead (see change log #9). |
| OQ-13 | Competitor-review evidence gathered 6 Sep 2026 (desk research: app-store listings, independent reviews, comparison articles); see updated section 1 and the source list at the end of this document. |
| OQ-7 | Kept all three leaderboard metrics; no duty-of-care change beyond what already exists. Per-metric hiding (FR-39) already lets a user hide the volume tab specifically if they don't want weight-based comparisons visible. |
| OQ-9 | A static offensive-word blocklist at username creation is the only moderation control in v1; reporting (FR-42) stays deferred to P2. Sufficient for thesis scope. |
| OQ-11 | Age verification is **self-declaration only** (checkbox at registration, US-01); no stronger verification (ID upload etc.), which would be disproportionate for a thesis project. |

## 10. Timeline Considerations

**Project framing.** This is an engineering thesis, so "timeline" here means *build order and scope discipline*, not a market launch schedule or a fixed deadline. No submission date is tracked in this document; section 10 gives a dependency-ordered build sequence that can be shortened by moving items down from P0 to P1/P2 if time runs short, using the order already given in the P1 backlog and P2 list as the cut line.

**Constraints**

- AI Act Art. 50 transparency obligations for limited-risk systems are already in force (since 2 Aug 2026); NFR-27–NFR-30 and FR-57 must exist before the AI generator can be demonstrated as compliant.
- GDPR obligations apply from the first real (or realistic test) user data, so the Privacy centre (FR-55/56) and consent flow (US-01) should be built early, not bolted on.
- Remaining blocking open question: **OQ-15** (iOS PWA timer/vibration spike), needed before FR-15. All other originally-blocking questions (OQ-1, OQ-2, OQ-3, OQ-5's shape, OQ-12, OQ-16, OQ-17) were resolved during the review pass. OQ-5's exact numeric values are still pending expert review but no longer block starting implementation.

**Build order for the 35 P0 requirements**

Grouped by dependency, not by "module," since P0 now spans every functional area:

| Step | Scope | Depends on |
|---|---|---|
| 1. Foundation | Account (FR-01–04), Privacy centre and export (FR-55/56), deletion with grace period (FR-05) | OQ-2, OQ-3, OQ-11 |
| 2. Exercise data | Starter library (FR-51), browse/search (FR-18/20), library data model (Appendix B) | — |
| 3. Plan generation | Questionnaire (FR-06), AI generator (FR-07), plan modification (FR-08), plan view (FR-47), transparency screen (FR-57), plan archive (FR-11), training-restriction declaration (FR-43) | step 2 (all design questions resolved) |
| 4. Logging | Start/log/edit/end session (FR-12–14, FR-16), mobility block (FR-46) | step 3 |
| 5. Progression | Rule-based suggestion (FR-10, Appendix A) | OQ-5, step 4 |
| 6. Adaptation | Skipped-session adaptation (FR-09) | step 4 |
| 7. Insight | Progress charts (FR-22), personal records (FR-24) | step 4–5 |
| 8. Streak | Streak (FR-30), needed by the leaderboard in step 9 | step 4 |
| 9. Social | Social profile, friend requests, remove/block, leaderboard, opt-in, sharing controls, friend profile (FR-32–36, FR-38, FR-39, FR-53) | OQ-3, step 8 |
| 10. Hardening | AI endpoint limits (NFR-39), retention jobs (NFR-40), notifications (NFR-41), audit log (NFR-17) | steps 1–9 |

**P1 backlog** (implemented if time allows, roughly in this order): manual plan (FR-48), rest timer (FR-15, pending OQ-15), cardio block (FR-45), calendar (FR-26), aggregate statistics (FR-23), CSV export (FR-27), muscle-group balance (FR-25), badges (FR-29, Appendix C), personal goals (FR-31), body-weight chart (FR-52), favourites (FR-50), custom exercises (FR-21), exercise substitution (FR-49), session note (FR-17), leaderboard history (FR-37), social notifications (FR-40), preview as friend (FR-54), PDF report (FR-28).

**P2 (designed for, not built)**: ML-based progression (FR-41), injury-aware plan generation (FR-44), report user (FR-42), 2FA (NFR-16). Interfaces should not preclude these (e.g. FR-10's rule engine and a future model share an interface, per FR-41).

## 11. Glossary

| Term | Definition |
|---|---|
| MVP | Minimum Viable Product: smallest feature set that delivers user value and validates assumptions. |
| Progression | Gradual increase of training load (weight, volume, intensity) to drive adaptation. |
| Expert-rules engine | Deterministic decision logic written by domain experts; not trained on data. |
| Training volume | Sets × reps × weight: total work done. |
| 1RM | One-repetition maximum, estimated from formulas. |
| RPE | Rate of Perceived Exertion, 1–10 subjective difficulty scale. |
| Streak | Number of consecutive calendar weeks in which the user met their session target (US-35). |
| GDPR / RODO | EU General Data Protection Regulation (Polish: Rozporządzenie o Ochronie Danych Osobowych). |
| AI Act | Regulation (EU) 2024/1689 laying down harmonised rules on artificial intelligence. |
| DPIA | Data Protection Impact Assessment (GDPR Art. 35). |
| PWA | Progressive Web App: installable web application. |
| WCAG | Web Content Accessibility Guidelines. |
| P0 / P1 / P2 | Must have / should have / future consideration (MoSCoW-equivalent). |

---

## Appendix A: Progression rule set (v0.1, draft)

Deterministic rules used by FR-10 and for starting weights in FR-07. Every suggestion stores the rule-set version. Values are proposals pending domain-expert review (OQ-5).

**Methodology note.** The rule set is grounded in three well-established, non-proprietary strength-training principles rather than invented from scratch:

- **Double progression** (increase reps to the top of a target range before increasing load, then drop back to the bottom of the range at the new load) is a standard novice/early-intermediate progression model widely described in strength-training literature (e.g. NSCA introductory programming, Rippetoe/Baechle-style novice programs).
- **Starting-load ratios as a fraction of body weight**, scaled down for lower experience, reflect the common coaching heuristic of estimating a conservative first-session load from body weight and self-reported experience rather than testing a true max on day one, appropriate for a beginner-only product that explicitly avoids 1RM testing.
- **A ~10% deload after repeated failure or a long break** is a conventional, widely used autoregulation heuristic to reduce injury risk without requiring a coach.

This grounds the *shape* of the rules. The specific numbers in tables A1–A5 (exact ratios, rep-range boundaries, increment sizes) are the thesis author's estimates and are explicitly flagged for domain-expert review (OQ-5) before being presented as validated advice; they are appropriate for a v0.1 implementation and for demonstrating the mechanism, not as certified training guidance.

### A1. Starting weights (first plan, no history)

Starting load = *body weight × ratio*, rounded down to the nearest available increment (2.5 kg barbell, 1 kg/2 kg dumbbell, nearest band level). Week 1 uses 90 % of this value (calibration week).

| Exercise pattern | Never trained | < 6 months | 6–12 months | Returning (≥ 6 mo away) |
|---|---|---|---|---|
| Squat (barbell) | 0.40 | 0.55 | 0.70 | 0.50 |
| Deadlift (barbell) | 0.50 | 0.70 | 0.90 | 0.60 |
| Bench press (barbell) | 0.30 | 0.40 | 0.55 | 0.40 |
| Overhead press (barbell) | 0.20 | 0.25 | 0.35 | 0.25 |
| Barbell row | 0.30 | 0.40 | 0.55 | 0.40 |
| Dumbbell compound (per hand) | 0.10 | 0.15 | 0.20 | 0.15 |
| Dumbbell isolation (per hand) | 0.05 | 0.08 | 0.10 | 0.08 |
| Machine / cable | 0.30 | 0.40 | 0.50 | 0.40 |
| Bodyweight / band | n/a (reps or band level from table A4) | | | |

Exercises are mapped to a pattern in the library (FR-19). Barbell loads never go below the empty bar (20 kg); if the computed value is lower, the exercise is substituted with its dumbbell or bodyweight variant.

### A2. Rep ranges and increments

| Exercise class | Rep range | Increment | Deload |
|---|---|---|---|
| Lower-body compound (squat, deadlift, lunge, hip hinge) | 6–10 | +5 kg barbell / +2 kg per dumbbell | −10 % |
| Upper-body compound (bench, row, press, pull-up) | 6–10 | +2.5 kg barbell / +1 kg per dumbbell | −10 % |
| Isolation (curl, extension, raise, fly) | 10–15 | +2.5 kg barbell / +1 kg per dumbbell / +1 machine step | −10 % |
| Bodyweight | 6–15 | +1 rep per set, then harder variant | easier variant |
| Band | 10–15 | next band level | previous band level |

### A3. Progression decision (double progression)

Evaluated after each completed session, per exercise, using the last **two** performances at the same load.

1. **Increase**: all sets in both of the last two performances reached the top of the rep range (and, if RPE was logged, average RPE ≤ 8): suggest load + increment. Rationale shown: "You hit the top of the rep range twice, time to add weight."
2. **Hold**: at least one set was inside the range but below the top, in either performance: suggest same load. Rationale: "Keep the weight and aim for more reps."
3. **Deload**: in both of the last two performances at least one set fell below the bottom of the range: suggest load × 0.9, rounded to increment. Rationale: "Two hard sessions in a row, drop the weight slightly to rebuild."
4. **Missed session**: if the last performance is older than 14 days: suggest load × 0.9 once, then normal rules. Rationale: "It's been a while, easing back in."
5. **Insufficient history**: fewer than two performances at the current load: show current load, rationale "Not enough history yet."

Rules are evaluated top to bottom; the first match applies. A user override (US-07) is recorded but does not change the rule outcome next time: the rules use logged performances, not accepted suggestions.

### A4. Bodyweight and band progressions

Each bodyweight and band exercise in the library defines an ordered ladder of variants (e.g. incline push-up → push-up → decline push-up → archer push-up). Rule 1 advances to the next ladder step instead of adding load; rule 3 steps back.

### A5. Plan-level weekly progression (generator, FR-07)

The generator sets a target of the **bottom** of the rep range in week 1 and the **top** by the last week, with a 1-week deload (−10 % load, same reps) after every 4th week for plans of 8+ weeks. Weekly targets are guidance only; actual suggestions come from A3.

---

## Appendix B: Exercise library data model

### B1. Exercise types

Type determines the logging widget and the progression branch. Adding an exercise of an existing type requires data only, no code.

| Type | Logged per set / block | Progression rule | Examples |
|---|---|---|---|
| **Weighted** | reps × weight | load increment (A2/A3) | barbell squat, dumbbell row, cable fly, machine press |
| **Bodyweight** | reps (+ optional added weight) | rep target → next ladder step (A4) | push-up, pull-up, dip, lunge |
| **Band** | reps × band level | next band level (A4) | band row, band pull-apart |
| **Timed hold** | seconds (+ optional weight) | +5 s target → next ladder step | plank, dead hang, wall sit |
| **Cardio** | duration + optional distance + optional RPE | none in v1 | run, bike, row, walk |
| **Mobility** | duration or number of holds | none | hip-flexor stretch, foam-roll quads, cat-cow |

### B2. Fields per exercise

| Group | Field | Required | Notes |
|---|---|---|---|
| Identity | id | yes | stable, never reused |
| | name_pl, name_en | yes | both searchable |
| | aliases | no | list, any language |
| | type | yes | one of B1 |
| Classification | movement_pattern | yes (strength types) | squat, hinge, horizontal push, horizontal pull, vertical push, vertical pull, lunge, carry, core, isolation |
| | exercise_class | yes (strength types) | lower compound / upper compound / isolation / bodyweight / band / timed (drives A2) |
| | primary_muscles | yes (strength, mobility) | list |
| | secondary_muscles | no | list |
| | difficulty | yes | 1–3; generator uses ≤ 2 |
| Equipment | equipment | yes | one of: barbell, dumbbell, kettlebell, machine, cable, band, bodyweight, pull-up bar, bench, cardio-machine, none |
| | environment | derived | gym / home / both, from equipment |
| Progression | starting_weight_row | yes (weighted) | row of table A1 |
| | increment_override | no | overrides A2 increment |
| | ladder_id, ladder_position | yes (bodyweight, band, timed) | ordered variant ladder |
| Content | technique_start, technique_movement, technique_mistakes | yes | ≤ 3 bullets each |
| | beginner_tip | no | one sentence |
| | media | yes | animation or static illustration; media_type recorded |
| Meta | is_custom, owner_user_id | system | custom exercises are private |
| | archived | system | hidden but history kept |

### B3. Starter library composition (FR-51)

Target 45–55 entries, every field filled. Minimum coverage so that the generator can build a complete plan for **each** environment:

- Every movement pattern has at least one gym (barbell/machine/cable) and one home (dumbbell/band/bodyweight) exercise at difficulty ≤ 2.
- Every bodyweight, band and timed-hold exercise belongs to a ladder with at least two steps.
- Mobility entries cover hips, thoracic spine, shoulders, hamstrings, ankles (warm-up building blocks).
- Cardio entries cover at least one no-equipment option (walk/run) and one machine option.

---

## Appendix C: Launch badges (FR-29)

Each badge is awarded once, automatically, when the condition first becomes true. Conditions are evaluated at session end unless stated otherwise.

| # | Badge | Category | Unlock condition |
|---|---|---|---|
| 1 | First rep | Getting started | First session ended as "done" |
| 2 | Plan in hand | Getting started | First plan (AI or manual) activated |
| 3 | Full week | Consistency | First calendar week meeting the streak target (US-35) |
| 4 | One month strong | Consistency | Streak reaches 4 weeks |
| 5 | Quarter club | Consistency | Streak reaches 12 weeks |
| 6 | Comeback | Consistency | Session completed after ≥ 14 days without one |
| 7 | New record | Progress | First heaviest-set PR (US-34) |
| 8 | Record collector | Progress | 10 PRs of any kind |
| 9 | Goal reached | Progress | A personal goal (US-17) achieved |
| 10 | Plan complete | Progress | Last week of a plan finished with ≥ 80 % of planned strength sessions done |
| 11 | Century | Volume | 100 sets logged in total (all strength types) |
| 12 | Home base | Variety | First session where every exercise has a home-compatible equipment tag |
| 13 | Heart rate | Variety | First cardio block logged |
| 14 | Better together | Social | First accepted friend (evaluated on friend acceptance) |
| 15 | Top of the board | Social | Ranked first on any friend leaderboard at week end, with ≥ 2 friends (evaluated at week rollover) |

Badges 14–15 require an enabled social profile. Categories map to `BadgeCategory` in the gamification class diagram.

---

## Sources (OQ-13 competitor evidence, gathered 6 September 2026)

Desk research only: app-store listings, independent reviews and comparison articles, not primary user interviews.

- Hevy vs Strong App: The Differences That Actually Matter (gymnoteplus.com/blog/hevy-vs-strong)
- Hevy vs Strong App (2026) (setgraph.app/ai-blog/hevy-vs-strong-app-comparison-2026)
- Strong App Alternative That Keeps Your Lift History (setgraph.app/articles/best-strong-app-alternatives-(2025))
- Fitbod App Review 2026: Honest Take After Real Testing (indiehackers.com/post/fitbod-app-review-2026-honest-take-after-real-testing-45d5f07a1b)
- Fitbod Review: 5 Things Nobody Tells You (fitnesstoolsreviewed.com/app-reviews/fitbod-review-is-the-ai-gym-app-worth-it)
- Fitbod App Review 2026: Cost, AI Workouts & Results (autonomous.ai/ourblog/fitbod-app-review)
- Fitbod vs JEFIT: Which Fitness App Delivers Results? (gymbird.com/fitness-apps/fitbod-vs-jefit)
- JEFIT Workout App Review (dr-muscle.com/jefit-workout-app-review)
- Fitbod vs Jefit: Weight Lifting Apps Comparison (dr-muscle.com/fitbod-vs-jefit)
- Fitbod Review 2024 by Independent Expert (dr-muscle.com/fitbod-app-review-alternative)
- App Store listings for Hevy, OneMoreRep, Alpha Progression, StepLoad, Milo of Croton, Progressive Overload Workout (apps.apple.com)

# Design

## Source of truth
- Status: Active
- Last refreshed: 2026-08-14
- Primary product surfaces: WPF shell (sidebar + page + log), Applications, Tweaks, Config, Cleanup, Updates, Settings
- Evidence reviewed: `src/OpenWindowUtility.App/App.xaml`, `MainWindow.xaml`, page views, WPF-UI Dark theme

## Brand
- Personality: Quiet Windows workshop tool. Direct, calm, no marketing voice.
- Trust signals: Admin-first, reversible tweaks, scan-before-delete cleanup, restore point default.
- Avoid: AI purple, neon, glassmorphism, Inter, icon-only mystery nav, exclamation-mark copy.

## Product goals
- Goals: Fast setup and maintenance of a Windows PC from one elevated app.
- Non-goals: Win11 ISO creator (v2), light theme in v1, extra package managers.
- Success signals: User finds the current section without hovering; primary action is obvious; log stays out of the way.

## Personas and jobs
- Primary personas: Enthusiasts and technicians setting up Windows 10/11 machines.
- User jobs: Install a catalog of apps, apply tweaks, repair Windows, clean junk, pin update policy.
- Key contexts of use: Full-screen desktop, always elevated, often on a fresh install.

## Information architecture
- Primary navigation: Labeled left rail — Applications, Tweaks, Config, Cleanup, Updates; Settings at the bottom.
- Core routes/screens: Same six pages as today. Win11 Creator stays hidden until v2.
- Content hierarchy: Page title → filters/actions → content → one-line footer. Job log collapsed by default.

## Design principles
- Labels over mystery icons.
- One accent. Semantic color only for FOSS, installed, deep/risk, danger.
- Space over extra boxes. Cards exist when a group needs a surface, not around every row.
- Tradeoffs: Dense enough for a catalog of ~80 apps; not a cockpit of unlabeled glyphs.

## Visual language
- Color: Canvas `#12141A`, rail `#0E1014`, surface `#1A1D24`, hairline `#2A2F3A`, text `#E8EAED`, muted `#8B919C`, accent `#4A7D76`.
- Typography: Segoe UI Variable Text, fallback Segoe UI. Titles 22/600, body 13, meta 11. Tabular figures on sizes/counts.
- Spacing/layout rhythm: 8px base. Page padding 24. Rail 208px. Tile 8px radius.
- Shape/radius/elevation: 8px containers, 6px controls. No drop shadows. 1px hairline borders.
- Motion: Hover/press only (~160ms). No load choreography.
- Imagery/iconography: App mark is an open four-pane window in teal on charcoal. Fluent SymbolIcon 20px in the rail. Catalog logos on tiles.

## Components
- Existing: WPF-UI buttons, text boxes, title bar, toggle switch.
- New/changed: `NavButton` (labeled + selected), `Card`, `PageTitle`, `AppTile`, brand mark in rail and window icon.
- Variants and states: Nav default / hover / selected / disabled. Tile hover hairline uses accent.
- Token/component ownership: Colors and shared styles live in `App.xaml`.

## Accessibility
- Target standard: WCAG AA contrast on text and selected nav.
- Keyboard/focus behavior: Keep WPF focus visuals; do not remove focus rectangles.
- Contrast/readability: Muted text stays above 4.5:1 on canvas.
- Screen-reader semantics: Nav buttons expose loc names, not only icons.
- Reduced motion: No decorative animation to disable.

## Responsive behavior
- Supported: Desktop 1024×640 and up. Not a mobile app.
- Layout adaptations: Detail pane 300px; tiles wrap.
- Touch/hover: Hover on tiles and nav; checkboxes remain  the selection target.

## Interaction states
- Loading: Job log lines; existing job runner.
- Empty: Search can yield an empty wrap panel (unchanged).
- Error: MessageBox for admin/catalog failures; log for job failures.
- Success: Log “Finished”.
- Disabled: Win11 Creator omitted; edition-unsupported features stay dimmed.

## Content voice
- Tone: Short, literal, bilingual EN/VI.
- Terminology: Cleanup, not “optimize PC”. Download on detail pane still installs via WinGet.
- Microcopy rules: Sentence case. No “Oops”. Confirm before destructive cleanup.

## Implementation constraints
- Framework/styling system: WPF + WPF-UI 4.3. No new NuGet.
- Design-token constraints: Resource keys in `App.xaml`.
- Performance constraints: WrapPanel tiles unchanged (virtualization out of scope).
- Compatibility constraints: Windows 10 22H2 / Windows 11 x64.
- Test/screenshot expectations: Existing Core tests; visual check of shell + Applications.

## Open questions
- [ ] Light theme — deferred
- [ ] Custom ISO (Win11 Creator) — v2

---
name: Lutra
description: A dark native Windows cooling instrument desk.
# Portable px notation encodes WPF device-independent pixels (DIPs), not physical screen pixels.
colors:
  accent: "#72D3AE"
  water: "#6DC5EA"
  amber: "#E4C584"
  violet: "#B8ABEA"
  danger: "#F29A8F"
  workspace: "#101715"
  surface: "#18221F"
  raised: "#202D28"
  rail: "#0C1210"
  ink: "#ECF3EF"
  muted: "#A0B2A8"
  line: "#304139"
  selection: "#2B4639"
  thumb: "#60786B"
  rail-meta: "#C1CFC7"
  nav-text: "#FFFFFF"
typography:
  reading:
    fontFamily: "Segoe UI"
    fontSize: "38px"
    fontWeight: 400
  reading-compact:
    fontFamily: "Segoe UI"
    fontSize: "32px"
    fontWeight: 400
  rpm:
    fontFamily: "Segoe UI"
    fontSize: "34px"
    fontWeight: 400
  rpm-compact:
    fontFamily: "Segoe UI"
    fontSize: "28px"
    fontWeight: 400
  headline:
    fontFamily: "Segoe UI"
    fontSize: "30px"
    fontWeight: 600
  brand:
    fontFamily: "Segoe UI"
    fontSize: "25px"
    fontWeight: 600
  section-large:
    fontFamily: "Segoe UI"
    fontSize: "22px"
    fontWeight: 600
  section:
    fontFamily: "Segoe UI"
    fontSize: "19px"
    fontWeight: 600
  section-compact:
    fontFamily: "Segoe UI"
    fontSize: "18px"
    fontWeight: 600
  instrument-title:
    fontFamily: "Segoe UI"
    fontSize: "17px"
    fontWeight: 600
  title:
    fontFamily: "Segoe UI"
    fontSize: "16px"
    fontWeight: 600
  duty-compact:
    fontFamily: "Segoe UI"
    fontSize: "15px"
    fontWeight: 600
  item-title:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 600
  prose:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 400
  body:
    fontFamily: "Segoe UI"
    fontSize: "13px"
    fontWeight: 400
  action:
    fontFamily: "Segoe UI"
    fontSize: "13px"
    fontWeight: 600
  label:
    fontFamily: "Segoe UI"
    fontSize: "12px"
    fontWeight: 400
  status:
    fontFamily: "Segoe UI"
    fontSize: "12px"
    fontWeight: 600
  annotation:
    fontFamily: "Segoe UI"
    fontSize: "11px"
    fontWeight: 400
  micro:
    fontFamily: "Segoe UI"
    fontSize: "10px"
    fontWeight: 400
rounded:
  square: "0px"
  check: "2px"
  control: "3px"
spacing:
  fine: "4px"
  label-gap: "6px"
  action-gap: "8px"
  compact-inset: "12px"
  field-gap: "13px"
  chart-inset: "14px"
  instrument-inset: "16px"
  section-gap: "18px"
  surface-inset: "20px"
  pane-gap: "22px"
  about-inset: "24px"
  shell-inline: "30px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.workspace}"
    typography: "{typography.action}"
    rounded: "{rounded.control}"
    padding: "8px 13px"
  button-secondary:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "8px 13px"
  button-compact:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "5px 10px"
  range-selected:
    backgroundColor: "{colors.selection}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
  text-field:
    backgroundColor: "{colors.workspace}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  select-field:
    backgroundColor: "{colors.workspace}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "7px 32px 7px 9px"
  checkbox:
    backgroundColor: "{colors.workspace}"
    textColor: "{colors.ink}"
    rounded: "{rounded.check}"
    size: "17px"
  checkbox-checked:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.workspace}"
    rounded: "{rounded.check}"
    size: "17px"
  navigation-rail:
    backgroundColor: "{colors.rail}"
    rounded: "{rounded.square}"
    width: "176px"
  navigation-item-selected:
    backgroundColor: "{colors.selection}"
    textColor: "{colors.nav-text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "11px 8px 11px 12px"
  surface-panel:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.surface-inset}"
  live-instrument:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.instrument-inset}"
  live-instrument-compact:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.compact-inset}"
  output-instrument:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.instrument-inset}"
  output-instrument-compact:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.compact-inset}"
  trend-panel:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted}"
    rounded: "{rounded.square}"
    padding: "{spacing.chart-inset}"
  trend-panel-compact:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted}"
    rounded: "{rounded.square}"
    padding: "10px"
  data-row:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    height: "37px"
    padding: "0px 12px"
  data-header:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.muted}"
    typography: "{typography.action}"
    height: "36px"
    padding: "0px 12px"
  curve-plot:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.muted}"
    typography: "{typography.annotation}"
  about-panel:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.about-inset}"
  brand-mark:
    size: "32px"
---

# Design System: Lutra

## Overview

**Creative North Star: "Dark graphite cooling instrument desk"**

Lutra is a native Windows cooling instrument for a desk in dim lighting. Deep graphite surrounds slightly lighter working surfaces; mint identifies deliberate actions, while cyan, amber and violet distinguish measured signals. The original twin-channel L mark and Segoe UI remain the visual identity.

The owner explicitly replaced the earlier light work area with this dark direction. Monitoring now gives temperature and flow dials, actual RPM/PWM readings and simultaneous trends priority. Curve editing remains a precise workbench with explicit draft state. Flat surfaces, ruled data and direct labels keep the application readable without decorative motion.

**Key Characteristics:**

- Dark surfaces throughout the application, including input wells, dropdowns and scrollbars.
- Mint action emphasis and a labeled cyan/amber/violet measurement vocabulary.
- Large tabular readouts with a compact native layout for short windows.
- Temperature and RPM traces visible together, with gaps and unavailable values preserved.
- Original vector L mark, native Segoe UI and English/Finnish copy.
- Explicit draft, connection and on-request update states.

This records the implemented 0.5 design on 14 October 2026. Frontmatter values are normative for this document. Its portable `px` strings represent **WPF device-independent pixels (DIPs)**: one logical 1/96 inch, scaled by Windows rather than assumed to be a physical pixel.

Authority is the current [WPF theme](src/Lutra.App/Theme.xaml), [UI helpers](src/Lutra.App/Ui.cs), [shell](src/Lutra.App/MainWindow.cs), [monitor](src/Lutra.App/MonitorView.cs), [About & updates](src/Lutra.App/AboutView.cs), [other views](src/Lutra.App/Views.cs), [curve form](src/Lutra.App/CurveForm.cs) and [plots](src/Lutra.App/Plot.cs). The [owner-approved direction](.impeccable/direction.md) establishes the dark world. Old literal color strings routed through `Ui.Color` are compatibility aliases; their resolved dark values, not the old literals, are recorded here.

Rendered evidence comprises the eight `v05-*.png` captures in [.impeccable/review](.impeccable/review): Overview in English and Finnish, Curves, Alarms, History, Devices, Settings and About & updates. The refreshed Overview captures show both plots in the initial content viewport at requested window sizes (1120 × 720 and 1440 × 920 DIPs). The refreshed About capture shows the demo-mode explanation. PNGs contain content without Windows chrome and use synthetic data. Source/image hashes, dimensions and preview limitations are in [.impeccable/design.json](.impeccable/design.json). Earlier light-theme PNGs remain historical evidence and do not define the current system.

## Colors

The palette has a dark graphite foundation, readable pale text and a small set of functional signal colors.

### Primary

- **Instrument mint** (`accent`) fills primary actions, checked controls and output bars; it also marks keyboard focus and curve/history traces.
- Primary action text uses **deep graphite** (`workspace`), keeping mint buttons legible against dark panels.

### Secondary

- **Coolant cyan** (`water`) identifies the coolant instrument.
- **Measurement amber** (`amber`) identifies other temperature instruments and the live curve cursor; the same native value also supplies warning text.
- **Flow violet** (`violet`) identifies the flow instrument.
- Monitoring traces cycle through cyan, amber, violet and mint by series index. Temperature and RPM charts have their own labeled legends; a color is not a universal hardware-channel identifier.

### Tertiary

- **Alarm coral** (`danger`) highlights alarm and validation feedback.

### Neutral

- **Deep graphite**, **instrument surface** and **raised graphite** (`workspace`, `surface`, `raised`) form the background, panel and control/popup layers.
- **Navigation graphite** (`rail`) separates persistent navigation without a bright edge.
- **Soft white** and **sage gray** (`ink`, `muted`) distinguish primary readings from labels, units, axes and supporting text.
- **Instrument rule** (`line`) supplies borders, plot guides, dial tracks and output-bar tracks.
- **Selected graphite** (`selection`) marks the chosen page, rule, dropdown option or data row.
- **Scroll grip** (`thumb`) makes the dark scrollbar visible at rest; hover uses muted and dragging uses mint.
- **Rail metadata** and **navigation white** (`rail-meta`, `nav-text`) preserve the explicitly implemented footer and navigation foregrounds.

**The Meaning Before Color Rule.** Name each signal and state in text. Mint identifies actions and plotted response; it does not assert verified hardware safety. A trace's legend is its identity.

The sidecar's eight-step tonal ramps are derived previews, not additional application brushes.

## Typography

**Interface font:** Segoe UI throughout. The implementation uses normal and semibold weights, native wrapping and default line metrics. It declares no alternate font, custom tracking or fixed line-height system. Main sensor and RPM readings explicitly use WPF `FontNumeralAlignment.Tabular`.

### Hierarchy

| Role | Normal / compact size | Use |
| --- | --- | --- |
| Sensor reading | 38 / 32 DIPs, regular | Temperature and flow values; tabular numerals. |
| RPM reading | 34 / 28 DIPs, regular | Actual rotational speed; tabular numerals. |
| Page title | 30 DIPs, semibold | Current page. |
| Rail wordmark | 25 DIPs, semibold | Lutra beside the original mark. |
| Large section | 22 DIPs, semibold | Update heading and empty-instrument prompt. |
| Section | 19 / 18 DIPs, semibold | Device/settings sections and Overview group headings. |
| Instrument title | 17 / 16 DIPs, semibold | Sensor names; the 17-DIP role also titles the curve plot. |
| Output name | 16 / 14 DIPs, semibold | Rule/output identity. |
| Duty percentage | 17 / 15 DIPs, semibold | Actual PWM measurement. |
| Explanatory prose | 14 DIPs, regular | About/update descriptions and empty states. |
| Body / action | 13 DIPs, regular / semibold | Default controls, body copy and primary actions. |
| Label / status | 12 DIPs, regular / semibold | Captions, legends, units and connection state. |
| Annotation | 11 DIPs, regular | Draft state, curve/history axes and rail description. |
| Small plot annotation | 10 DIPs, regular | Dial scale and simultaneous-trend axes. |

This is the observed role set, not a geometric type scale. About uses a local larger identity lockup (34-DIP semibold wordmark, 48-DIP mark), a mint version number (20 DIPs) and a regular tagline (17 DIPs); these are local identity treatments, not new defaults for other screens.

**The Native Reading Rule.** Use Segoe UI and the implemented regular/semibold hierarchy. Main instrument and RPM values use tabular numeral alignment; labels retain native text behavior.

## Layout

The native window starts at (1440 × 920 DIPs) and has a minimum size of (1120 × 720 DIPs). The rail remains (176 DIPs) wide. Main body margins are left/right (30 DIPs), top (24 DIPs) and bottom (16 DIPs). The header has (23 DIPs) below it; the footer has (16 DIPs) above it.

A page body scrolls vertically below the title and connection/draft status. Horizontal page scrolling is disabled. Revert/Apply stay outside this scroller and are hidden on clean Overview, About, History and Devices pages; editing pages retain their actions. This is a desktop layout, not a web/mobile shell.

### Overview density

Overview has a native **window-height threshold (900 DIPs)**. Below it, the same instruments become compact in place; resizing does not rebuild live history. The rail and side-by-side plot structure remain.

| Element | Window height below 900 DIPs | Window height at least 900 DIPs |
| --- | --- | --- |
| Sensor/output inset | 12 DIPs | 16 DIPs |
| Dial | 72 × 76 DIPs | 110 × 114 DIPs |
| Dial column, including its right gap | 84 DIPs | 122 DIPs |
| Sensor note minimum height | 0 DIPs | 32 DIPs |
| Section header top/bottom margins | 10 / 8 DIPs | 22 / 12 DIPs |
| Overview toolbar button padding | 10 horizontal / 5 vertical DIPs | 13 horizontal / 8 vertical DIPs |
| Overview toolbar button minimum height | 28 DIPs | 34 DIPs |
| Trend panel inset | 10 DIPs | 14 DIPs |
| Each trend plot height | 120 DIPs | `clamp(windowHeight − 750, 150, 190)` DIPs |

At the default height, each trend plot is (170 DIPs). Up to three temperature/flow instruments share a UniformGrid. Enabled outputs use one column for one rule, three for three rules, otherwise two. Adjacent instrument edges are separated by (12 DIPs). Temperature and RPM panels are equal-width columns with a gap (12 DIPs); full sensor readings sit in an expander below the trends. These arrangements describe Overview, not every future page.

### Editing and information pages

Curves retain a fixed rule column (180 DIPs, including its right margin of 20 DIPs), a flexible working panel and an inspector column (230 DIPs, including a left margin of 22 DIPs). The curve plot minimum height is (240 DIPs). Input and output selectors sit above the graph/inspector pair.

Most working panels use an inset (20 DIPs). About uses two equal, top-aligned panels with insets (24 DIPs) and a gap (22 DIPs); project links and update buttons use wrapping rows. Settings also uses two equal columns with a gap (22 DIPs). History retains its larger plot (380 DIPs). Labels leave (6 DIPs) above the control and field groups leave (13 DIPs) below.

English and Finnish are present in the rendered evidence. Values use the selected locale, including Finnish decimal commas. This document does not infer untested large-text or other-device behavior from these captures.

## Elevation & Depth

Graphite tones, spacing and thin rules establish depth. General work panels and chart panels have ruled edges; the monitoring instrument surfaces are borderless. There are no application shadow, blur or glass tokens. The native window requests dark title-bar treatment; the content captures do not include Windows chrome.

**The Ruled Surface Rule.** Use graphite tone, whitespace and thin rules for depth. Do not add application drop shadows, glass effects or decorative entrance animation.

Control state changes are immediate. Buttons use a border change on hover, reduced opacity while pressed or disabled, and an overlaid focus outline. Dropdown popup animation is explicitly disabled. Readings and plots update in place; no custom easing or animation-duration tokens are declared.

## Shapes

Working surfaces and charts are square. Button, TextBox, ComboBox and scroll-thumb templates use a small radius (3 DIPs). Checkbox boxes use a radius (2 DIPs). Ordinary control/panel borders use (1 DIP); button focus is an overlay (2 DIPs), so focusing does not change its padding.

The twin-channel L retains its original (32 × 32 DIP) design canvas and round-capped strokes (4 DIPs). Its lower mint path is `M7,5 L7,20 Q7,25 12,25 L27,25`; the upper soft-white path is `M16,5 L16,15 Q16,17 18,17 L27,17`. It renders at (32 DIPs) in the rail and (48 DIPs) on About.

Dials are circular instruments with a (270-degree) sweep beginning at 135 degrees. Their visual role is measurement; the rest of the interface keeps rectangular working surfaces.

## Components

### Buttons and range selection

Compact text actions with restrained shape.

- **Primary:** mint fill/border, workspace text, semibold type.
- **Secondary:** raised graphite fill, ink text and line border.
- **Default sizing:** padding (13 horizontal / 8 vertical DIPs), minimum height (34 DIPs). Compact Overview toolbars use the values in Layout; minimum height is not a fixed height.
- **States:** hover changes the border to muted; pressed body opacity is (0.72); disabled body opacity is (0.45). Keyboard focus overlays a mint border (2 DIPs).
- **Selected range:** selected graphite fill plus mint border. Options are 5 minutes, 15 minutes and 1 hour, followed by All history. Selection loads local history; it is not a decorative pill system.

### Fields, selectors and checks

The application now owns dark WPF templates for these controls.

- **TextBox:** workspace well, ink text/caret, line border and control radius; padding (9 horizontal / 7 vertical DIPs), minimum height (34 DIPs), helper minimum width (60 DIPs). Text selection uses mint at opacity (0.35). Hover changes the edge to muted; focus changes it to mint; disabled opacity is (0.45).
- **ComboBox:** workspace well and the same radius/edge. Padding is left/top/right/bottom (9/7/32/7 DIPs), leaving room for an inline vector chevron. Its raised-graphite popup has a line border, a top gap (3 DIPs), no popup animation and maximum height (340 DIPs). Highlighted/selected options use selected graphite.
- **Checkbox:** a (17 × 17 DIP) box, workspace at rest and mint when checked, with a dark vector check. The label follows a (19-DIP) box column and (8-DIP) content margin. Hover/focus use the ink border; focus thickens it to (2 DIPs).
- **Labels:** a WPF Label targets its field and the helper supplies an automation name. Unavailable selections remain explicitly labeled rather than silently choosing a different device.

### Navigation, work panels and tables

The persistent rail has left-aligned text buttons with transparent default fill and selected-graphite current-page fill. Navigation padding is left/top/right/bottom (12/11/8/11 DIPs), with (5 DIPs) between items. Seven pages include About & updates. Selected curve rules reuse the light-text/selected-graphite treatment on an instrument surface.

Working panels use surface fill, square corners and a line border. Tables use surface rows, raised headers/alternating rows, horizontal rules and full-row single selection. Row height is (37 DIPs), header height (36 DIPs), and horizontal cell padding (12 DIPs). Overview's expanded table reserves (95 DIPs) for value and (65 DIPs) for unit. No chip/tag component family is implemented.

Dark scrollbars are (12 DIPs) wide, with a thumb inset (3 DIPs). Tooltips and WPF menus use raised graphite, ink and line borders. Windows/WinForms tray menus are separate platform UI and are not claimed to use the WPF template.

### Live temperature and flow instrument

A borderless surface combines a named dial, large tabular value, unit and state note. The coolant instrument uses cyan, other temperature instruments amber, and flow violet. Default dial maxima are coolant (60 °C), other temperature (100 °C) and flow (200 l/h); these are display ranges, not universal safety limits.

The dial track and value arc have round caps and a width (7 DIPs). A startup threshold uses an ink tick (2 DIPs); smaller scale ticks use the line brush (1 DIP). Missing data removes the value arc and shows an em dash with an unavailable label. Alarm/unavailable notes use amber. Values and automation names update without rebuilding the view.

### Output RPM and PWM instrument

A borderless surface presents the output/rule name, mint duty percentage, actual RPM, unit, a thin duty bar (5 DIPs) and a state sentence. Compact and regular type/insets follow Layout and Typography. The bar uses mint over a line-colored track.

Fresh zero is shown as zero with stopped/start-threshold text. Missing output shows an em dash and a dimmed empty bar (opacity 0.25). An unmapped RPM remains an em dash with “RPM not mapped”, even when PWM is available. The code only assigns an RPM-to-output relationship where its device mapping is explicit; it does not estimate RPM from duty.

### Simultaneous monitoring trends

Two adjacent plots show temperature and RPM together. Each has named colored legends, local-time labels, a unit and horizontal guides. Traces use (2-DIP) strokes; guides use (0.6 DIP). A recent endpoint receives a dot with radius (3 DIPs).

The monitor merges local history with live measurements for the chosen range. Null/nonfinite values and gaps longer than fifteen seconds break the line. Recent dots appear only for samples less than ten seconds old. Empty plots state that measurements are awaited. Static preview curves in the sidecar are illustrations; they do not assert live hardware behavior.

### Curve editor and longer history

The curve editor keeps sixteen nodes, a mint response line (2.5 DIPs), unselected surface-filled nodes with a mint edge (1.5 DIPs) and radius (3.5 DIPs), and a filled selected node with radius (5 DIPs). A dashed muted startup line differs from the amber live-temperature cursor. Keyboard focus draws a dashed mint rectangle (2 DIPs).

Left/right select a point; up/down edit response. Dragging and labeled numeric fields remain available. Pending point/endpoint edits use amber notices and require their explicit update action before Apply. Longer history uses the shared dark axes and a mint trace (2 DIPs), preserving gaps.

### Devices and GPU sensor discovery

The Devices page begins with a GPU sensor recovery action, visible even when no graphics card is detected. The secondary button is labeled “Rediscover GPU sensors” or “Tunnista GPU-anturit uudelleen”. A short explanation links it to missing temperatures after a driver update and states that curves remain unchanged.

The button disables during its request, settings application, software updates, disconnection and demo mode. Inline text distinguishes waiting, discovery restarted and failure; a polite automation live region exposes that feedback. A restarted discovery does not claim that readings have returned. The background service retains control and its configured sensor-loss fallback. The action never captures or applies a draft. Tests cover these states and both languages; a live button integration check confirmed the request and fresh GPU readings with the same service process.

### About & updates

The identity panel carries the current version and project, release and issue links. The adjacent update panel has a preview-release checkbox, an explicit status, check/install actions and conditional cancellation. Release notes appear when a release is available.

Checking, available/latest, downloading/verifying, cancellation, failure and update-result text are distinct states. Check/install are disabled in demo mode with a specific explanation. Normal noninstalled builds may check releases; installation requires the packaged installed application. Dirty edits require apply/revert before installation. These are source-observed interface states; screenshot evidence covers the demo state, not a completed installation.

Ten self-contained HTML/CSS previews accompany this document. They approximate the native components for the design panel. WPF remains authoritative for input, automation, popup behavior, rendering, DPI and update actions; preview buttons do not control hardware or install software.

## Do's and Don'ts

### Do:

- **Do** reuse the implemented dark brushes and WPF templates before adding a visual variant.
- **Do** preserve simultaneous temperature and RPM trends in the supported Overview layouts.
- **Do** distinguish zero, unavailable and unmapped readings with explicit values and text; preserve history gaps.
- **Do** keep numeric instrument and RPM values tabular, with their units and signal names visible.
- **Do** keep selected sensor, output and editable response legible together on the curve workbench.
- **Do** retain labels, keyboard focus, pending-coordinate feedback and separate draft/apply state.
- **Do** keep the running version, available release and download/install status distinct, and check updates only on request.
- **Do** preserve the original twin-channel L paths and their mint/soft-white treatment.

### Don't:

- **Don't** restore the superseded porcelain work area or forest-green-on-white action theme.
- **Don't** substitute estimated RPM for an unavailable or unmapped measurement.
- **Don't** encode a signal or alarm solely through color.
- **Don't** imitate Aquasuite visual assets or add RGB decoration.
- **Don't** turn the Overview height threshold into a web/mobile architecture or a rule for every page.
- **Don't** treat demo screenshots or illustrative preview data as evidence of physical hardware behavior.
- **Don't** promote synthetic tonal ramps or HTML preview behavior into native implementation rules.
- **Don't** imply that editing a coordinate, downloading a package or viewing a new version number has applied a curve or completed an update.

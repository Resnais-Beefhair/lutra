---
name: Lutra
description: A precise native Windows cooling control desk.
# Portable px notation below represents WPF device-independent pixels (DIPs), not physical pixels.
colors:
  forest: "#256450"
  focus-green: "#287F62"
  mint-mark: "#99CDB7"
  ink: "#202A28"
  muted: "#596763"
  porcelain: "#FFFFFF"
  workspace: "#F0F2F0"
  graphite: "#252E2C"
  rail-selected: "#43564F"
  rail-meta: "#D0DDD7"
  rail-subtle: "#B4C6BE"
  line: "#D9DFDC"
  selection: "#DFEBE4"
  text-selection: "#C5DED4"
  table-header: "#EFF3F0"
  table-alternate: "#F7F9F7"
  chart-grid: "#DFE5E1"
  live-amber: "#A4631D"
  amber-ink: "#854D12"
  device-caution: "#8B4D19"
  alarm-red: "#9B352C"
typography:
  headline:
    fontFamily: "Segoe UI"
    fontSize: "30px"
    fontWeight: 600
  brand:
    fontFamily: "Segoe UI"
    fontSize: "25px"
    fontWeight: 600
  section:
    fontFamily: "Segoe UI"
    fontSize: "19px"
    fontWeight: 600
  section-compact:
    fontFamily: "Segoe UI"
    fontSize: "18px"
    fontWeight: 600
  plot-title:
    fontFamily: "Segoe UI"
    fontSize: "17px"
    fontWeight: 600
  item-title:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 600
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
rounded:
  square: "0px"
  control: "3px"
spacing:
  label-gap: "6px"
  action-gap: "8px"
  compact-gap: "10px"
  cell-inset: "12px"
  field-gap: "13px"
  form-gap: "16px"
  section-gap: "18px"
  surface-inset: "20px"
  pane-gap: "22px"
  shell-top: "24px"
  shell-inline: "30px"
components:
  button-primary:
    backgroundColor: "{colors.forest}"
    textColor: "{colors.porcelain}"
    typography: "{typography.action}"
    rounded: "{rounded.control}"
    padding: "8px 13px"
  button-secondary:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "8px 13px"
  text-field:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    padding: "7px 9px"
  select-field:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    padding: "6px 8px"
  navigation-rail:
    backgroundColor: "{colors.graphite}"
    rounded: "{rounded.square}"
    width: "176px"
  navigation-item:
    backgroundColor: "transparent"
    textColor: "{colors.porcelain}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "11px 8px 11px 12px"
  navigation-item-selected:
    backgroundColor: "{colors.rail-selected}"
    textColor: "{colors.porcelain}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "11px 8px 11px 12px"
  surface-panel:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.ink}"
    rounded: "{rounded.square}"
    padding: "{spacing.surface-inset}"
  data-row:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    height: "37px"
    padding: "0px 12px"
  data-header:
    backgroundColor: "{colors.table-header}"
    textColor: "{colors.muted}"
    typography: "{typography.action}"
    height: "36px"
    padding: "0px 12px"
  rule-selector-selected:
    backgroundColor: "{colors.selection}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "8px 13px"
  curve-plot:
    backgroundColor: "{colors.porcelain}"
    textColor: "{colors.muted}"
    typography: "{typography.annotation}"
  brand-mark:
    size: "32px"
---

# Design System: Lutra

## Overview

**Creative North Star: "Graphite-and-porcelain control desk"**

Lutra is a precise, approachable native Windows instrument. Porcelain working surfaces sit on a cool gray workspace, with a graphite navigation rail and restrained forest-green actions. The original twin-channel L mark gives the application its own identity; the interface relies on readable controls and plotted evidence rather than decorative imagery.

The visual system is compact and flat. Segoe UI, ruled rows, aligned fields and generous chart space make device relationships and editable values easy to scan. Native controls retain their Windows behavior, and state is stated in text alongside color. The owner approved this direction and delegated its name and visual identity.

**Key Characteristics:**

- Graphite navigation, porcelain working panels and forest-green action emphasis.
- Native Segoe UI with a compact regular/semibold hierarchy.
- Square ruled surfaces, small button corners and visible keyboard focus.
- Prominent plots with distinct response, startup and live-temperature marks.
- Explicit draft, unavailable-reading and pending-coordinate states.

This is the implemented Windows WPF system, documented on 14 October 2026. The frontmatter is normative for the values recorded here. Its portable `px` strings encode **WPF device-independent pixels (DIPs)**, where one DIP is one logical 1/96 inch; actual device pixels depend on Windows scaling. No web or mobile layout is specified.

Source authority: [Theme.xaml](src/Lutra.App/Theme.xaml), [Ui.cs](src/Lutra.App/Ui.cs), [MainWindow.cs](src/Lutra.App/MainWindow.cs), [Views.cs](src/Lutra.App/Views.cs), [CurveForm.cs](src/Lutra.App/CurveForm.cs), and the `CurvePlot` / `HistoryPlot` classes in [Plot.cs](src/Lutra.App/Plot.cs). The approved [direction](.impeccable/direction.md) supplies the name of the world; the implementation supplies the values.

Rendered evidence checked: all seven PNGs in [.impeccable/review](.impeccable/review), covering Curves in English and Finnish, Overview, Alarms, History, Devices and Settings. They show demo data and default states. Their filenames identify the requested window size; the PNGs contain the content region without Windows chrome. Hashes, actual image dimensions and component provenance are recorded in [.impeccable/design.json](.impeccable/design.json). This document records the design system; it does not expand the finish review's four resolved findings into a whole-application certification.

## Colors

A cool neutral field keeps dense controls readable; green belongs to deliberate actions and plotted response. Warm annotations carry a different meaning from the response line.

### Primary

- **Forest green** (`forest`) fills primary actions and draws response/history traces. Connection text also uses this brush, with the connection mode written out.
- **Focus green** (`focus-green`) identifies keyboard focus on custom buttons.
- **Mint channel** (`mint-mark`) is the lower channel of the original L mark, paired with porcelain.

### Secondary

- **Live cursor amber** (`live-amber`) marks the live temperature cursor and its baseline dot.
- **Amber annotation** (`amber-ink`) labels live temperature and pending numeric coordinate edits.
- **Device caution** (`device-caution`) is reserved for a device limitation message.

### Tertiary

- **Alarm red** (`alarm-red`) identifies active alarm or sensor-unavailable alarm text. The label supplies the specific condition.

### Neutral

- **Graphite ink** and **quiet gray-green** (`ink`, `muted`) separate primary text from field labels, supporting copy and axes.
- **Porcelain** and **cool workspace** (`porcelain`, `workspace`) distinguish work panels from the surrounding window.
- **Graphite rail** and **rail selection** (`graphite`, `rail-selected`) establish navigation and the current page; `rail-meta` and `rail-subtle` form the footer hierarchy.
- **Ruled edge** (`line`) supplies panel boundaries, secondary-button strokes and table rules.
- **Selected surface** and **text selection** (`selection`, `text-selection`) are separate treatments for a chosen rule/data row and selected input text.
- **Table heading**, **alternate row** and **chart ruling** (`table-header`, `table-alternate`, `chart-grid`) structure dense information without shadows.

**The Meaning Before Color Rule.** Green identifies actions and plotted response. State must also be named in text; green alone never asserts verified hardware safety.

The sidecar's eight-step tonal ramps are synthesized palette previews. They are labeled as derived and are not additional application brushes.

## Typography

**Interface font:** Segoe UI throughout, including the wordmark text, controls and chart annotations. No custom fallback stack, fixed line-height, tracking override, font-feature token or alternate font is declared by the application. WPF supplies the native defaults.

### Hierarchy

| Role | Frontmatter token | Implemented use |
| --- | --- | --- |
| Page headline | `headline` | Main page title; semibold (30 DIPs). |
| Wordmark | `brand` | Lutra beside the vector mark; semibold (25 DIPs). |
| Section heading | `section` / `section-compact` | Device/settings headings (19 DIPs) and overview headings (18 DIPs), semibold. |
| Plot title | `plot-title` | Curve response heading; semibold (17 DIPs). |
| Item title | `item-title` | Curve names, active-control names and protection subheading; semibold (14 DIPs). |
| Body | `body` | Default controls, page subtitles and explanatory text; regular (13 DIPs). |
| Action | `action` | Primary button and table headings; semibold (13 DIPs). |
| Label / status | `label` / `status` | Field captions and supporting copy (12 DIPs); connection status is semibold. |
| Annotation | `annotation` | Axis labels, draft status, rule state and secondary rail footer; regular (11 DIPs). |

This is an observed set of sizes, not a geometric type scale. Measurements align through stable table columns; the code does not declare a tabular-numeral OpenType override. The empty-curve prompt uses a local semibold heading (22 DIPs), not another shared type role.

**The Native Type Rule.** Use the implemented Segoe UI hierarchy and normal Windows text behavior. Do not introduce a separate display or monospace face for future screens.

## Layout

The shell is a native resizable desktop window: initial size (1440 × 920 DIPs), minimum size (1120 × 720 DIPs). A fixed navigation rail (176 DIPs) sits beside the main body. Body margins are left/right (30 DIPs), top (24 DIPs) and bottom (16 DIPs). The header separates from content by (23 DIPs); the bottom action strip separates from content by (16 DIPs).

The title and connection/draft status sit above a vertically scrolling page body. Revert and Apply sit below it, aligned right. The page body uses an automatic vertical scrollbar with horizontal scrolling disabled. There is no breakpoint-driven alternate shell. At narrower supported widths the flexible graph area shrinks while the rail and inspector keep their widths; content beyond the viewport remains available by vertical scrolling.

The current curve surface uses a rule-list column (180 DIPs, including its right margin of 20 DIPs), a flexible white editing panel, and an inspector column (230 DIPs, including its left margin of 22 DIPs). The graph has a minimum height (240 DIPs). Input and output selectors precede the graph/inspector split. Overview uses a fixed active-controls column (310 DIPs) beside the readings; Settings uses two equal columns separated by (22 DIPs). These are implemented surface arrangements, not requirements that every future page must copy.

Reusable spacing is recorded in the frontmatter. Most panels use padding (20 DIPs), paired form fields use gaps (10–18 DIPs), field labels sit above controls with a gap (6 DIPs), and field groups leave (13 DIPs) below them. Text wraps through the shared TextBlock style. English and Finnish are evidenced; no untested text-scale or device-class behavior is inferred.

## Elevation & Depth

The application is flat. White work panels, the cool workspace and the dark rail create tonal depth; thin borders and horizontal rules define boundaries. No application drop-shadow token, blur, glass layer or elevation animation is implemented. Windows owns native window chrome, dropdowns and other platform affordances; their appearance is not a custom Lutra elevation system.

**The Ruled Surface Rule.** Separate working areas with tone, whitespace and thin rules. Application panels do not use drop shadows.

State changes are immediate. The button template changes opacity on hover and when disabled; there are no custom easing or duration tokens. Telemetry refresh is data behavior, not decorative motion.

## Shapes

Working panels and ruled data are square. The custom button border has a small corner radius (3 DIPs); the primary, secondary, navigation and rule-list buttons all inherit this template. Panel and ordinary button borders use a thin stroke (1 DIP). Keyboard-focused button borders become (2 DIPs). TextBox and ComboBox retain their native templates; no custom radius for them is asserted.

The twin-channel L is vector geometry on a (32 × 32 DIP) design canvas. Both strokes have round caps and a stroke width (4 DIPs). The lower mint path is `M7,5 L7,20 Q7,25 12,25 L27,25`; the upper porcelain path is `M16,5 L16,15 Q16,17 18,17 L27,17`. The rail uses the mark at (32 DIPs). Preserve these paths rather than substituting a letter glyph or unrelated icon.

## Components

### Buttons

Compact, restrained actions with readable text.

- **Primary:** forest fill and border, porcelain text, semibold type; use the `button-primary` token.
- **Secondary:** porcelain fill, ink text and ruled edge; use `button-secondary`.
- **Sizing:** horizontal/vertical padding (13/8 DIPs), minimum height (34 DIPs); the minimum is not a fixed height.
- **States:** mouse-over sets the template border opacity to (0.8); disabled sets it to (0.45). Keyboard focus uses the focus-green border (2 DIPs). There is no additional custom pressed animation.
- **Meaning:** Apply is gated by draft/service state and pending coordinates. Disabled appearance is a state treatment, not another palette color.

### Inputs and selectors

Native fields inside a quiet labeled form.

- **TextBox:** porcelain background, ink text, ruled edge, text-selection brush; padding (9/7 DIPs), minimum height (34 DIPs), helper-created minimum width (60 DIPs). Keyboard focus changes its border to forest.
- **ComboBox:** the theme sets porcelain, ink and ruled-edge brushes, padding (8/6 DIPs), minimum height (34 DIPs) and a maximum dropdown height (340 DIPs). The native WPF template supplies the rendered bevel, arrow, popup and platform states.
- **Labels:** muted text above the field; a WPF Label targets the control, and the helper sets its automation name.
- **Checkbox:** native Windows checkbox with vertical margins (5 DIPs). Its checked state is not replaced by a decorative toggle.
- **Unavailable option:** a missing selected device/sensor remains visible as an explicitly unavailable entry; it is not silently displayed as a different choice.

### Navigation and rule selection

The rail uses text buttons with left-aligned content, porcelain text and a transparent default fill. The current page uses rail-selection fill. Button padding is left/top/right/bottom (12/11/8/11 DIPs), with (5 DIPs) between items. Navigation inherits the shared hover, focus and disabled treatments.

The curve list uses the same button template on a light surface. A selected rule receives selected-surface fill; its semibold name and smaller enabled/disabled label remain separate. Selection is both tonal and contextual, not a floating card effect.

### Working panels

Porcelain containers with a square ruled edge and standard surface padding. They group a meaningful task, form or chart. The helper creates no shadow, clipping effect or radius. Adjacent data may remain directly on the workspace, as in Overview.

### Measurement and device tables

The read-only DataGrid uses horizontal rules, full-row single selection and separate columns for stable scanning. Rows are (37 DIPs), column headers (36 DIPs), and cell/header horizontal padding is (12 DIPs). Alternating rows use the alternate-row fill; selected cells use selected-surface fill and ink. The value column in Overview is fixed (88 DIPs) with a separate unit column (65 DIPs).

Missing readings are an em dash. Native scrolling remains visible when table content exceeds its available area. No chip/tag system is present.

### Curve editor and history plot

The response plot is a custom WPF FrameworkElement, not a chart-library skin. A white plot carries horizontal guides, muted axes, a forest response line and sixteen editable nodes. The response stroke is (2.5 DIPs); unselected nodes are white with a forest edge (1.5 DIPs) and radius (3.5 DIPs). The selected node is filled forest with radius (5 DIPs). A dashed muted startup line differs from the amber live-temperature line and caption.

The plot is keyboard focusable. Focus draws a dashed forest rectangle (2 DIPs); left/right choose a point, and up/down change response. Dragging and the labeled numeric coordinate fields provide additional editing paths. Numeric point and endpoint edits show a pending notice and require their explicit update action before Apply. The plot's automation peer announces the selected point and its coordinates.

History uses the same axes with a forest trace (2 DIPs). Missing values break the line. Empty history uses in-plot text. Overview and History provide different chart heights (230 and 380 DIPs) for their different layouts; these are not responsive breakpoints.

### State and feedback

Connection/demo mode and draft state are written at the top right. Feedback occupies the bottom strip near the application actions. Pending curve-coordinate edits use amber text beside the editor; active alarm and unavailable-sensor alarm labels use alarm red. These texts are part of the operational hierarchy, not promotional badges.

The sidecar provides ten self-contained HTML/CSS component previews. They approximate the native appearance for the design panel; WPF code remains the authority for templates, input behavior, automation, DPI rendering and platform focus/selection details. The preview's curve data is illustrative and does not execute the native editor.

## Do's and Don'ts

### Do:

- **Do** reuse the frontmatter tokens and the existing WPF theme before adding a visual variant.
- **Do** keep labels associated with their controls and retain visible keyboard focus.
- **Do** keep the selected sensor, output and response curve legible together.
- **Do** preserve text for draft, unavailable, pending and alarm states; show missing measurements as an em dash and history gaps as gaps.
- **Do** keep the page body scrollable while the page title, connection status and bottom actions remain outside that scroller.
- **Do** preserve the original twin-channel L geometry and its mint/white treatment on graphite.
- **Do** use the approved native Segoe UI family and regular/semibold weights.

### Don't:

- **Don't** use green as a claim of verified cooling safety or universal hardware compatibility.
- **Don't** replace the workbench with the rejected oversized dark-blue metric-card wall.
- **Don't** add RGB decoration, borrowed Aquasuite visual assets, glass effects or decorative entrance animation.
- **Don't** convert native DIP values into assumed physical-pixel sizes or invent web/mobile breakpoints.
- **Don't** promote demo measurements, synthesized tonal ramps or browser preview behavior into native product rules.
- **Don't** imply that edited point coordinates have reached the curve or hardware before the explicit update and apply actions.


// ===========================================================================
// PanelWireViewer  --  Dear ImGui + SQLite tool to look up every wired
// connection to/from a selected part of the CNC control panel.
//
//   * Pick a part from the dropdown -> see all its connections (wire number,
//     From, To, signal/description, wire spec), with adjustable text size.
//   * Data lives in a local SQLite DB (panelwire.db), seeded on first run from
//     the A11 wiring rev 0.7 netlist (src/seed_data.h, made by gen_seed.py).
//     The CRUD tabs edit Wires and Parts.
// ===========================================================================
#include "db.h"

#include "imgui.h"
#include "imgui_impl_glfw.h"
#include "imgui_impl_opengl3.h"
#include <GLFW/glfw3.h>
#include <imgui-node-editor/imgui_node_editor.h>

#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <map>
#include <string>
#include <vector>

namespace ed = ax::NodeEditor;

// ---------------------------------------------------------------------------
// Global state
// ---------------------------------------------------------------------------
static Db g_db;
static std::vector<Part> g_parts;
static int  g_selPartIdx = -1;
static std::vector<Wire> g_viewWires;
static float g_textScale = 1.3f;
static char  g_partSearch[128] = "";
static bool  g_hideLanded = false;
static std::string g_status;

// Tables size every column to its content (SizingFixedFit) and scroll
// horizontally if the total is wider than the view. kTableFlags also scrolls
// vertically; kTableFlagsGrouped omits ScrollY for tables stacked inside an
// already-scrolling parent (the Build Progress per-area tables).
static const ImGuiTableFlags kTableFlags =
    ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable |
    ImGuiTableFlags_ScrollX | ImGuiTableFlags_ScrollY | ImGuiTableFlags_SizingFixedFit;
static const ImGuiTableFlags kTableFlagsGrouped =
    ImGuiTableFlags_Borders | ImGuiTableFlags_RowBg | ImGuiTableFlags_Resizable |
    ImGuiTableFlags_ScrollX | ImGuiTableFlags_SizingFixedFit;

// Node-editor (read-only connection graph) state.
static ed::EditorContext* g_edCtx = nullptr;          // per-part connection graph
static ed::Config         g_edConfig;
static int                g_graphLaidOutFor = -2;     // part id the graph is laid out for

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static bool InputTextStd(const char* label, std::string& s,
                         ImGuiInputTextFlags flags = 0) {
    char buf[1024];
    std::strncpy(buf, s.c_str(), sizeof(buf));
    buf[sizeof(buf) - 1] = '\0';
    if (ImGui::InputText(label, buf, sizeof(buf), flags)) { s = buf; return true; }
    return false;
}

static std::string toLower(std::string s) {
    std::transform(s.begin(), s.end(), s.begin(),
                   [](unsigned char c) { return (char)std::tolower(c); });
    return s;
}

static std::vector<std::string> splitTokens(const std::string& s) {
    std::vector<std::string> out;
    size_t start = 0;
    while (start <= s.size()) {
        size_t end = s.find(';', start);
        if (end == std::string::npos) end = s.size();
        std::string t = s.substr(start, end - start);
        size_t a = t.find_first_not_of(" \t");
        size_t b = t.find_last_not_of(" \t");
        if (a != std::string::npos) out.push_back(t.substr(a, b - a + 1));
        start = end + 1;
    }
    return out;
}

// Which side(s) of the wire does this part sit on? -> "From", "To", or "Both".
static const char* sideLabel(const Part& p, const Wire& w) {
    bool inFrom = false, inTo = false;
    std::string lf = toLower(w.from_text), lt = toLower(w.to_text);
    for (const auto& tok : splitTokens(p.match)) {
        std::string lk = toLower(tok);
        if (!lk.empty()) {
            if (lf.find(lk) != std::string::npos) inFrom = true;
            if (lt.find(lk) != std::string::npos) inTo = true;
        }
    }
    if (inFrom && inTo) return "Both";
    if (inFrom) return "From";
    if (inTo)   return "To";
    return "-";
}

static void refreshParts() {
    int prevId = (g_selPartIdx >= 0 && g_selPartIdx < (int)g_parts.size())
                     ? g_parts[g_selPartIdx].id : -1;
    g_parts = g_db.parts();
    g_selPartIdx = -1;
    for (int i = 0; i < (int)g_parts.size(); ++i)
        if (g_parts[i].id == prevId) { g_selPartIdx = i; break; }
    if (g_selPartIdx < 0 && !g_parts.empty()) g_selPartIdx = 0;
}

static void refreshView() {
    if (g_selPartIdx >= 0 && g_selPartIdx < (int)g_parts.size())
        g_viewWires = g_db.wiresForPart(g_parts[g_selPartIdx]);
    else
        g_viewWires.clear();
}

static void exportViewCsv() {
    if (g_selPartIdx < 0) return;
    const Part& p = g_parts[g_selPartIdx];
    std::string fn = "connections_" + p.name + ".csv";
    for (char& c : fn) if (c == '/' || c == '\\' || c == ' ') c = '_';
    std::ofstream f(fn);
    if (!f) { g_status = "Could not write " + fn; return; }
    f << "Landed,No.,Side,From,To,Signal,Wire\n";
    auto q = [](const std::string& s) {
        std::string o = "\"";
        for (char c : s) { if (c == '"') o += '"'; o += c; }
        o += "\""; return o;
    };
    for (const auto& w : g_viewWires)
        f << (w.done ? "yes" : "no") << ',' << q(w.number) << ',' << q(sideLabel(p, w))
          << ',' << q(w.from_text) << ',' << q(w.to_text) << ',' << q(w.signal)
          << ',' << q(w.wire_spec) << '\n';
    g_status = "Exported " + std::to_string(g_viewWires.size()) + " rows to " + fn;
}

// ---------------------------------------------------------------------------
// Viewer tab
// ---------------------------------------------------------------------------
static void drawViewer() {
    ImGui::TextUnformatted("Part:");
    ImGui::SameLine();
    ImGui::SetNextItemWidth(360);
    const char* preview =
        (g_selPartIdx >= 0 && g_selPartIdx < (int)g_parts.size())
            ? g_parts[g_selPartIdx].name.c_str()
            : "(select a part)";
    if (ImGui::BeginCombo("##partcombo", preview)) {
        std::string lastLoc;
        for (int i = 0; i < (int)g_parts.size(); ++i) {
            const Part& p = g_parts[i];
            if (p.location != lastLoc) {
                lastLoc = p.location;
                ImGui::SeparatorText(p.location.empty() ? "(no location)" : p.location.c_str());
            }
            std::string lbl = p.name + "  [" + p.category + "]";
            bool sel = (i == g_selPartIdx);
            if (ImGui::Selectable(lbl.c_str(), sel)) { g_selPartIdx = i; refreshView(); }
            if (sel) ImGui::SetItemDefaultFocus();
        }
        ImGui::EndCombo();
    }

    ImGui::SameLine();
    ImGui::TextUnformatted("   Text size:");
    ImGui::SameLine();
    ImGui::SetNextItemWidth(220);
    if (ImGui::SliderFloat("##textsize", &g_textScale, 0.7f, 3.0f, "%.2fx")) {
        g_db.setSetting("text_scale", std::to_string(g_textScale));
    }
    ImGui::SameLine();
    if (ImGui::Button("Export CSV")) exportViewCsv();

    if (g_selPartIdx >= 0 && g_selPartIdx < (int)g_parts.size()) {
        const Part& p = g_parts[g_selPartIdx];
        ImGui::Separator();
        ImGui::Text("%s", p.name.c_str());
        if (!p.spec.empty())        ImGui::TextDisabled("Spec: %s", p.spec.c_str());
        if (!p.description.empty())  ImGui::TextWrapped("%s", p.description.c_str());

        int total = (int)g_viewWires.size();
        int landed = 0;
        for (const auto& w : g_viewWires) if (w.done) ++landed;
        ImGui::Text("Connections: %d", total);
        ImGui::SameLine();
        ImGui::Text("   Landed: %d / %d", landed, total);
        ImGui::SameLine();
        ImGui::Checkbox("Hide landed", &g_hideLanded);
        if (total > 0) {
            ImGui::SameLine();
            ImGui::SetNextItemWidth(160);
            ImGui::ProgressBar((float)landed / (float)total, ImVec2(160, 0));
        }
    }

    ImGui::Separator();

    // Connections table -- its own child so SetWindowFontScale only resizes
    // the connection text (the variable text-size requirement).
    ImGui::BeginChild("conn", ImVec2(0, 0), false);
    ImGui::SetWindowFontScale(g_textScale);

    const Part* p = (g_selPartIdx >= 0) ? &g_parts[g_selPartIdx] : nullptr;
    std::string tableId = "conns_" + std::to_string(p ? p->id : -1);
    if (ImGui::BeginTable(tableId.c_str(), 7, kTableFlags)) {
        ImGui::TableSetupScrollFreeze(0, 1);
        ImGui::TableSetupColumn("OK");
        ImGui::TableSetupColumn("No.");
        ImGui::TableSetupColumn("Side");
        ImGui::TableSetupColumn("From");
        ImGui::TableSetupColumn("To");
        ImGui::TableSetupColumn("Signal / Description");
        ImGui::TableSetupColumn("Wire");
        ImGui::TableHeadersRow();

        for (auto& w : g_viewWires) {
            if (g_hideLanded && w.done) continue;
            ImGui::TableNextRow();
            if (w.done)
                ImGui::TableSetBgColor(ImGuiTableBgTarget_RowBg0, IM_COL32(40, 85, 40, 120));

            ImGui::TableNextColumn();
            ImGui::PushID(w.id);
            if (ImGui::Checkbox("##done", &w.done)) {
                std::string err;
                if (!g_db.setWireDone(w.id, w.done, err)) {
                    g_status = "Save failed: " + err;
                    w.done = !w.done;   // revert on failure
                }
            }
            ImGui::PopID();

            if (w.done) ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.number.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p ? sideLabel(*p, w) : "-");
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.from_text.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.to_text.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.signal.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.wire_spec.c_str());
            if (w.done) ImGui::PopStyleColor();
        }
        ImGui::EndTable();
    }
    ImGui::SetWindowFontScale(1.0f);
    ImGui::EndChild();
}

// ---------------------------------------------------------------------------
// Wires CRUD tab
// ---------------------------------------------------------------------------
static Wire g_editWire;
static bool g_wireIsNew = false;
static bool g_openWirePopup = false;
static int  g_deleteWireId = -1;
static char g_wireSearch[128] = "";

static void openWireEditor(const Wire& w, bool isNew) {
    g_editWire = w; g_wireIsNew = isNew; g_openWirePopup = true;
}

static void drawWiresCrud() {
    if (ImGui::Button("+ Add wire")) {
        Wire w; openWireEditor(w, true);
    }
    ImGui::SameLine();
    ImGui::SetNextItemWidth(260);
    ImGui::InputTextWithHint("##wsearch", "filter (number, from, to, signal, area)...",
                             g_wireSearch, sizeof(g_wireSearch));

    std::vector<Wire> list = g_db.wires(g_wireSearch);
    int landed = 0;
    for (const auto& w : list) if (w.done) ++landed;
    ImGui::Text("%d wires   (landed %d / %d)", (int)list.size(), landed, (int)list.size());

    if (ImGui::BeginTable("wires", 9, kTableFlags)) {
        ImGui::TableSetupScrollFreeze(0, 1);
        ImGui::TableSetupColumn("OK");
        ImGui::TableSetupColumn("No.");
        ImGui::TableSetupColumn("From");
        ImGui::TableSetupColumn("To");
        ImGui::TableSetupColumn("Signal");
        ImGui::TableSetupColumn("Wire");
        ImGui::TableSetupColumn("Area");
        ImGui::TableSetupColumn("Edit");
        ImGui::TableSetupColumn("Del");
        ImGui::TableHeadersRow();

        for (auto& w : list) {
            ImGui::TableNextRow();
            ImGui::PushID(w.id);
            ImGui::TableNextColumn();
            if (ImGui::Checkbox("##done", &w.done)) {
                std::string err;
                if (!g_db.setWireDone(w.id, w.done, err)) { g_status = "Save failed: " + err; w.done = !w.done; }
                else refreshView();
            }
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.number.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.from_text.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.to_text.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.signal.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.wire_spec.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(w.area.c_str());
            ImGui::TableNextColumn();
            if (ImGui::SmallButton("Edit")) openWireEditor(w, false);
            ImGui::TableNextColumn();
            if (ImGui::SmallButton("X")) { g_deleteWireId = w.id; ImGui::OpenPopup("del_wire"); }
            if (ImGui::BeginPopup("del_wire")) {
                ImGui::Text("Delete wire %s?", w.number.c_str());
                if (ImGui::Button("Yes, delete")) {
                    std::string err;
                    if (g_db.deleteWire(g_deleteWireId, err)) { g_status = "Wire deleted."; refreshView(); }
                    else g_status = "Delete failed: " + err;
                    ImGui::CloseCurrentPopup();
                }
                ImGui::SameLine();
                if (ImGui::Button("Cancel")) ImGui::CloseCurrentPopup();
                ImGui::EndPopup();
            }
            ImGui::PopID();
        }
        ImGui::EndTable();
    }

    if (g_openWirePopup) { ImGui::OpenPopup("Wire editor"); g_openWirePopup = false; }
    if (ImGui::BeginPopupModal("Wire editor", nullptr, ImGuiWindowFlags_AlwaysAutoResize)) {
        ImGui::SetNextItemWidth(120); InputTextStd("Number",  g_editWire.number);
        ImGui::SetNextItemWidth(420); InputTextStd("From",    g_editWire.from_text);
        ImGui::SetNextItemWidth(420); InputTextStd("To",      g_editWire.to_text);
        ImGui::SetNextItemWidth(420); InputTextStd("Signal / description", g_editWire.signal);
        ImGui::SetNextItemWidth(200); InputTextStd("Wire spec", g_editWire.wire_spec);
        ImGui::SetNextItemWidth(260); InputTextStd("Area",    g_editWire.area);
        ImGui::SetNextItemWidth(420); InputTextStd("Notes",   g_editWire.notes);
        ImGui::Checkbox("Landed (physically installed)", &g_editWire.done);
        ImGui::Separator();
        if (ImGui::Button("Save", ImVec2(120, 0))) {
            std::string err;
            bool ok = g_wireIsNew ? g_db.addWire(g_editWire, err)
                                  : g_db.updateWire(g_editWire, err);
            g_status = ok ? "Wire saved." : ("Save failed: " + err);
            if (ok) refreshView();
            ImGui::CloseCurrentPopup();
        }
        ImGui::SameLine();
        if (ImGui::Button("Cancel", ImVec2(120, 0))) ImGui::CloseCurrentPopup();
        ImGui::EndPopup();
    }
}

// ---------------------------------------------------------------------------
// Parts CRUD tab
// ---------------------------------------------------------------------------
static Part g_editPart;
static bool g_partIsNew = false;
static bool g_openPartPopup = false;
static int  g_deletePartId = -1;
static char g_partCrudSearch[128] = "";

static void openPartEditor(const Part& p, bool isNew) {
    g_editPart = p; g_partIsNew = isNew; g_openPartPopup = true;
}

static void drawPartsCrud() {
    if (ImGui::Button("+ Add part")) { Part p; openPartEditor(p, true); }
    ImGui::SameLine();
    ImGui::SetNextItemWidth(260);
    ImGui::InputTextWithHint("##psearch", "filter (name, category, location)...",
                             g_partCrudSearch, sizeof(g_partCrudSearch));
    ImGui::SameLine();
    ImGui::TextDisabled("(?)");
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip(
            "'Match' is a ';'-separated list of case-insensitive substrings.\n"
            "A wire is shown for this part if any token appears in its From or To text.\n"
            "Example for the A11 board:  A11");

    std::vector<Part> list = g_db.parts(g_partCrudSearch);
    ImGui::Text("%d parts", (int)list.size());

    if (ImGui::BeginTable("parts", 7, kTableFlags)) {
        ImGui::TableSetupScrollFreeze(0, 1);
        ImGui::TableSetupColumn("Name");
        ImGui::TableSetupColumn("Category");
        ImGui::TableSetupColumn("Location");
        ImGui::TableSetupColumn("Match tokens");
        ImGui::TableSetupColumn("Spec");
        ImGui::TableSetupColumn("Edit");
        ImGui::TableSetupColumn("Del");
        ImGui::TableHeadersRow();

        for (const auto& p : list) {
            ImGui::TableNextRow();
            ImGui::PushID(p.id);
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p.name.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p.category.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p.location.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p.match.c_str());
            ImGui::TableNextColumn(); ImGui::TextUnformatted(p.spec.c_str());
            ImGui::TableNextColumn();
            if (ImGui::SmallButton("Edit")) openPartEditor(p, false);
            ImGui::TableNextColumn();
            if (ImGui::SmallButton("X")) { g_deletePartId = p.id; ImGui::OpenPopup("del_part"); }
            if (ImGui::BeginPopup("del_part")) {
                ImGui::Text("Delete part '%s'?", p.name.c_str());
                if (ImGui::Button("Yes, delete")) {
                    std::string err;
                    if (g_db.deletePart(g_deletePartId, err)) { g_status = "Part deleted."; refreshParts(); refreshView(); }
                    else g_status = "Delete failed: " + err;
                    ImGui::CloseCurrentPopup();
                }
                ImGui::SameLine();
                if (ImGui::Button("Cancel")) ImGui::CloseCurrentPopup();
                ImGui::EndPopup();
            }
            ImGui::PopID();
        }
        ImGui::EndTable();
    }

    if (g_openPartPopup) { ImGui::OpenPopup("Part editor"); g_openPartPopup = false; }
    if (ImGui::BeginPopupModal("Part editor", nullptr, ImGuiWindowFlags_AlwaysAutoResize)) {
        ImGui::SetNextItemWidth(420); InputTextStd("Name",     g_editPart.name);
        ImGui::SetNextItemWidth(260); InputTextStd("Category", g_editPart.category);
        ImGui::SetNextItemWidth(200); InputTextStd("Location", g_editPart.location);
        ImGui::SetNextItemWidth(420); InputTextStd("Spec",     g_editPart.spec);
        ImGui::SetNextItemWidth(420); InputTextStd("Description", g_editPart.description);
        ImGui::SetNextItemWidth(420); InputTextStd("Match tokens (;-separated)", g_editPart.match);
        ImGui::Separator();
        if (ImGui::Button("Save", ImVec2(120, 0))) {
            std::string err;
            bool ok = g_partIsNew ? g_db.addPart(g_editPart, err)
                                  : g_db.updatePart(g_editPart, err);
            g_status = ok ? "Part saved." : ("Save failed: " + err);
            if (ok) { refreshParts(); refreshView(); }
            ImGui::CloseCurrentPopup();
        }
        ImGui::SameLine();
        if (ImGui::Button("Cancel", ImVec2(120, 0))) ImGui::CloseCurrentPopup();
        ImGui::EndPopup();
    }
}

// ---------------------------------------------------------------------------
// Build Progress tab -- every wire grouped by area, in build order.
// ---------------------------------------------------------------------------
static bool g_progHideLanded = false;
static int  g_progForce = -1;   // -1 = leave, 0 = collapse all, 1 = expand all

static void drawProgress() {
    std::vector<Wire> all = g_db.wires("");

    // First pass: ordered list of areas + per-area / overall counts.
    std::vector<std::string> areaOrder;
    std::map<std::string, int> tot, lan;
    int totalAll = (int)all.size(), landedAll = 0;
    for (const auto& w : all) {
        if (tot.find(w.area) == tot.end()) areaOrder.push_back(w.area);
        tot[w.area]++;
        if (w.done) { lan[w.area]++; landedAll++; }
    }

    ImGui::Text("Overall: %d / %d landed", landedAll, totalAll);
    ImGui::SameLine();
    ImGui::SetNextItemWidth(220);
    ImGui::ProgressBar(totalAll ? (float)landedAll / totalAll : 0.0f, ImVec2(220, 0));
    ImGui::SameLine(); if (ImGui::Button("Expand all"))   g_progForce = 1;
    ImGui::SameLine(); if (ImGui::Button("Collapse all")) g_progForce = 0;
    ImGui::SameLine(); ImGui::Checkbox("Hide landed wires", &g_progHideLanded);
    ImGui::SameLine();
    ImGui::SetNextItemWidth(170);
    if (ImGui::SliderFloat("Text size", &g_textScale, 0.7f, 3.0f, "%.2fx"))
        g_db.setSetting("text_scale", std::to_string(g_textScale));
    ImGui::Separator();

    ImGui::BeginChild("progbody", ImVec2(0, 0), false);
    ImGui::SetWindowFontScale(g_textScale);

    for (const auto& area : areaOrder) {
        int t = tot[area], l = lan[area];
        bool complete = (t > 0 && l == t);
        char label[256];
        std::snprintf(label, sizeof(label), "%s   [%d/%d]%s###%s",
                      area.c_str(), l, t, complete ? "  DONE" : "", area.c_str());

        if (g_progForce == 1)      ImGui::SetNextItemOpen(true);
        else if (g_progForce == 0) ImGui::SetNextItemOpen(false);

        if (complete) ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.45f, 0.85f, 0.45f, 1.0f));
        bool open = ImGui::CollapsingHeader(label);
        if (complete) ImGui::PopStyleColor();
        if (!open) continue;

        std::string tableId = "tbl_" + area;
        if (ImGui::BeginTable(tableId.c_str(), 6, kTableFlagsGrouped)) {
            ImGui::TableSetupColumn("OK");
            ImGui::TableSetupColumn("No.");
            ImGui::TableSetupColumn("From");
            ImGui::TableSetupColumn("To");
            ImGui::TableSetupColumn("Signal / Description");
            ImGui::TableSetupColumn("Wire");
            ImGui::TableHeadersRow();

            for (auto& w : all) {
                if (w.area != area) continue;
                if (g_progHideLanded && w.done) continue;
                ImGui::TableNextRow();
                if (w.done)
                    ImGui::TableSetBgColor(ImGuiTableBgTarget_RowBg0, IM_COL32(40, 85, 40, 120));

                ImGui::TableNextColumn();
                ImGui::PushID(w.id);
                if (ImGui::Checkbox("##done", &w.done)) {
                    std::string err;
                    if (!g_db.setWireDone(w.id, w.done, err)) { g_status = "Save failed: " + err; w.done = !w.done; }
                    else refreshView();
                }
                ImGui::PopID();

                if (w.done) ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.number.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.from_text.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.to_text.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.signal.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.wire_spec.c_str());
                if (w.done) ImGui::PopStyleColor();
            }
            ImGui::EndTable();
        }
    }

    ImGui::SetWindowFontScale(1.0f);
    ImGui::EndChild();
    g_progForce = -1;   // consume the expand/collapse request
}

// ---------------------------------------------------------------------------
// Landings tab -- reverse lookup: pick an endpoint (a terminal block, drive
// terminal, connector pin, ...) and see every wire landing on it. Endpoints
// are derived from the From/To text, so this needs no extra data entry.
// ---------------------------------------------------------------------------
static char        g_landSearch[128] = "";
static bool        g_landTbOnly = false;
static std::string g_landSel;

static bool isPlaceholderEndpoint(const std::string& s) {
    if (s.empty() || s == "-") return true;
    if (s[0] == '(') return true;            // "(spare)", "(Trunk B Cable 2 core 3)", ...
    return false;
}

// Terminal blocks ("TB...") and the rev 0.7 XL / XN distribution blocks.
static bool isBlockEndpoint(const std::string& epLow) {
    return epLow.find("tb") != std::string::npos || epLow.find("block") != std::string::npos;
}

static void drawLandings() {
    std::vector<Wire> all = g_db.wires("");

    // Distinct endpoints + how many wires touch each.
    std::map<std::string, int> eps;
    for (const auto& w : all) {
        if (!isPlaceholderEndpoint(w.from_text)) eps[w.from_text]++;
        if (!isPlaceholderEndpoint(w.to_text))   eps[w.to_text]++;
    }

    ImGui::SetNextItemWidth(300);
    ImGui::InputTextWithHint("##landsearch", "filter endpoints...",
                             g_landSearch, sizeof(g_landSearch));
    ImGui::SameLine();
    ImGui::Checkbox("Terminal blocks only", &g_landTbOnly);
    ImGui::SameLine();
    ImGui::SetNextItemWidth(150);
    if (ImGui::SliderFloat("Text size", &g_textScale, 0.7f, 3.0f, "%.2fx"))
        g_db.setSetting("text_scale", std::to_string(g_textScale));
    ImGui::SameLine();
    ImGui::TextDisabled("(click list, then Up/Down)");
    ImGui::Separator();

    // ---- left: endpoint list (click or Up/Down arrows to select) ----
    ImGui::BeginChild("eplist", ImVec2(360, 0), true);

    // The currently-visible (filtered) endpoints, in list order.
    std::vector<std::string> shown;
    std::string sLow = toLower(g_landSearch);
    for (const auto& kv : eps) {
        std::string epLow = toLower(kv.first);
        if (g_landTbOnly && !isBlockEndpoint(epLow)) continue;
        if (!sLow.empty() && epLow.find(sLow) == std::string::npos) continue;
        shown.push_back(kv.first);
    }

    // Arrow-key navigation when the list panel has focus.
    bool scrollToSel = false;
    if (ImGui::IsWindowFocused() && !shown.empty()) {
        int cur = -1;
        for (int i = 0; i < (int)shown.size(); ++i)
            if (shown[i] == g_landSel) { cur = i; break; }
        if (ImGui::IsKeyPressed(ImGuiKey_DownArrow)) {
            cur = (cur < 0) ? 0 : std::min(cur + 1, (int)shown.size() - 1);
            g_landSel = shown[cur]; scrollToSel = true;
        } else if (ImGui::IsKeyPressed(ImGuiKey_UpArrow)) {
            cur = (cur < 0) ? 0 : std::max(cur - 1, 0);
            g_landSel = shown[cur]; scrollToSel = true;
        }
    }

    for (const auto& kv : eps) {
        const std::string& ep = kv.first;
        std::string epLow = toLower(ep);
        if (g_landTbOnly && !isBlockEndpoint(epLow)) continue;
        if (!sLow.empty() && epLow.find(sLow) == std::string::npos) continue;
        std::string label = ep + "  (" + std::to_string(kv.second) + ")";
        bool sel = (ep == g_landSel);
        if (ImGui::Selectable(label.c_str(), sel)) g_landSel = ep;
        if (sel && scrollToSel) ImGui::SetScrollHereY(0.5f);
    }
    ImGui::EndChild();

    ImGui::SameLine();

    // ---- right: wires landing on the selected endpoint ----
    ImGui::BeginChild("epdetail", ImVec2(0, 0), false);
    ImGui::SetWindowFontScale(g_textScale);

    if (g_landSel.empty()) {
        ImGui::TextDisabled("Select an endpoint on the left to see everything landing on it.");
    } else {
        int total = 0, landed = 0;
        for (const auto& w : all)
            if (w.from_text == g_landSel || w.to_text == g_landSel) {
                ++total; if (w.done) ++landed;
            }
        ImGui::Text("%s", g_landSel.c_str());
        ImGui::Text("Landings: %d     Landed: %d / %d", total, landed, total);
        if (total == 0) ImGui::TextDisabled("(no wires land here anymore)");
        ImGui::Separator();

        std::string tableId = "landtbl_" + g_landSel;
        if (ImGui::BeginTable(tableId.c_str(), 6, kTableFlags)) {
            ImGui::TableSetupScrollFreeze(0, 1);
            ImGui::TableSetupColumn("OK");
            ImGui::TableSetupColumn("No.");
            ImGui::TableSetupColumn("Connects to (-> out / <- in)");
            ImGui::TableSetupColumn("Signal / Description");
            ImGui::TableSetupColumn("Wire");
            ImGui::TableSetupColumn("Area");
            ImGui::TableHeadersRow();

            for (auto& w : all) {
                bool isFrom = (w.from_text == g_landSel);
                bool isTo   = (w.to_text == g_landSel);
                if (!isFrom && !isTo) continue;
                const std::string& other = isFrom ? w.to_text : w.from_text;
                std::string conn = (isFrom ? "-> " : "<- ") + other;

                ImGui::TableNextRow();
                if (w.done)
                    ImGui::TableSetBgColor(ImGuiTableBgTarget_RowBg0, IM_COL32(40, 85, 40, 120));

                ImGui::TableNextColumn();
                ImGui::PushID(w.id);
                if (ImGui::Checkbox("##done", &w.done)) {
                    std::string err;
                    if (!g_db.setWireDone(w.id, w.done, err)) { g_status = "Save failed: " + err; w.done = !w.done; }
                    else refreshView();
                }
                ImGui::PopID();

                if (w.done) ImGui::PushStyleColor(ImGuiCol_Text, ImGui::GetStyleColorVec4(ImGuiCol_TextDisabled));
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.number.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(conn.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.signal.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.wire_spec.c_str());
                ImGui::TableNextColumn(); ImGui::TextUnformatted(w.area.c_str());
                if (w.done) ImGui::PopStyleColor();
            }
            ImGui::EndTable();
        }
    }

    ImGui::SetWindowFontScale(1.0f);
    ImGui::EndChild();
}

// ---------------------------------------------------------------------------
// Graph tab -- read-only node graph of the selected part's connections.
// Center node = the part; one node per connected wire (its far endpoint, wire
// number, spec, signal); links show wire direction and turn green when landed.
// View only: panning/zooming and dragging nodes for layout is allowed, but no
// links or nodes can be created or deleted (we never handle create/delete).
// ---------------------------------------------------------------------------
static void drawGraph() {
    // Part selector (shares g_selPartIdx with the Viewer).
    ImGui::TextUnformatted("Part:");
    ImGui::SameLine();
    ImGui::SetNextItemWidth(360);
    const char* preview =
        (g_selPartIdx >= 0 && g_selPartIdx < (int)g_parts.size())
            ? g_parts[g_selPartIdx].name.c_str() : "(select a part)";
    if (ImGui::BeginCombo("##gpartcombo", preview)) {
        std::string lastLoc;
        for (int i = 0; i < (int)g_parts.size(); ++i) {
            const Part& p = g_parts[i];
            if (p.location != lastLoc) {
                lastLoc = p.location;
                ImGui::SeparatorText(p.location.empty() ? "(no location)" : p.location.c_str());
            }
            std::string lbl = p.name + "  [" + p.category + "]";
            bool sel = (i == g_selPartIdx);
            if (ImGui::Selectable(lbl.c_str(), sel)) { g_selPartIdx = i; refreshView(); }
            if (sel) ImGui::SetItemDefaultFocus();
        }
        ImGui::EndCombo();
    }
    static bool zoomToFit = false;
    ImGui::SameLine();
    if (ImGui::Button("Re-layout")) g_graphLaidOutFor = -99;   // force re-place this frame
    ImGui::SameLine();
    if (ImGui::Button("Zoom to fit")) zoomToFit = true;        // applied inside Begin/End below
    ImGui::SameLine();
    ImGui::TextDisabled("(drag empty space to pan, scroll to zoom -- view only)");
    ImGui::Separator();

    if (g_selPartIdx < 0 || g_selPartIdx >= (int)g_parts.size()) {
        ImGui::TextDisabled("Select a part to see its connection graph.");
        return;
    }
    const Part& part = g_parts[g_selPartIdx];
    const int n = (int)g_viewWires.size();
    const bool doLayout = (g_graphLaidOutFor != part.id);

    ed::SetCurrentEditor(g_edCtx);
    ed::Begin("conn_graph", ImVec2(0, 0));

    // --- center node (the part) ---
    const int centerNode = 1, centerIn = 1000, centerOut = 1001;
    if (doLayout) ed::SetNodePosition(ed::NodeId(centerNode), ImVec2(0, 0));
    ed::BeginNode(ed::NodeId(centerNode));
        ed::BeginPin(ed::PinId(centerIn), ed::PinKind::Input);  ImGui::TextUnformatted("o"); ed::EndPin();
        ImGui::SameLine();
        ImGui::BeginGroup();
        ImGui::TextUnformatted(part.name.c_str());
        if (!part.category.empty()) ImGui::TextDisabled("%s", part.category.c_str());
        ImGui::EndGroup();
        ImGui::SameLine();
        ed::BeginPin(ed::PinId(centerOut), ed::PinKind::Output); ImGui::TextUnformatted("o"); ed::EndPin();
    ed::EndNode();

    // Classify each connected wire relative to the part:
    //   'out' = part is the wire's source (From/Both)  -> node on the RIGHT, red
    //   'in'  = part is the wire's destination (To)     -> node on the LEFT, default
    std::vector<char> isOut(n);
    int nIn = 0, nOut = 0;
    for (int i = 0; i < n; ++i) {
        const char* side = sideLabel(part, g_viewWires[i]);
        isOut[i] = (std::strcmp(side, "To") != 0);
        if (isOut[i]) ++nOut; else ++nIn;
    }

    const float xCol  = 420.0f;   // column distance left/right of the centre node
    const float pitch = 100.0f;   // vertical spacing within a column
    int seenIn = 0, seenOut = 0;

    for (int i = 0; i < n; ++i) {
        Wire& w = g_viewWires[i];
        const bool out = isOut[i];
        const std::string& far = out ? w.to_text : w.from_text;

        const int nodeId = 2 + i;
        const int inPin  = 2000 + i * 2;
        const int outPin = 2000 + i * 2 + 1;

        if (doLayout) {
            if (out) {
                float y = (seenOut - (nOut - 1) * 0.5f) * pitch;
                ed::SetNodePosition(ed::NodeId(nodeId), ImVec2(xCol, y));   // 'out' -> right
                ++seenOut;
            } else {
                float y = (seenIn - (nIn - 1) * 0.5f) * pitch;
                ed::SetNodePosition(ed::NodeId(nodeId), ImVec2(-xCol, y));  // 'in' -> left
                ++seenIn;
            }
        }

        // 'out' nodes are red; 'in' nodes keep the default node colour.
        if (out) {
            ed::PushStyleColor(ed::StyleColor_NodeBg,     ImVec4(0.42f, 0.12f, 0.12f, 1.0f));
            ed::PushStyleColor(ed::StyleColor_NodeBorder, ImVec4(0.90f, 0.35f, 0.35f, 1.0f));
        }
        ed::BeginNode(ed::NodeId(nodeId));
            ed::BeginPin(ed::PinId(inPin), ed::PinKind::Input);   ImGui::TextUnformatted("o"); ed::EndPin();
            ImGui::SameLine();
            ImGui::BeginGroup();
            ImGui::TextUnformatted(far.c_str());
            ImGui::TextDisabled("#%s   %s%s", w.number.c_str(), w.wire_spec.c_str(),
                                w.done ? "   [landed]" : "");
            if (!w.signal.empty()) ImGui::TextDisabled("%s", w.signal.c_str());
            ImGui::EndGroup();
            ImGui::SameLine();
            ed::BeginPin(ed::PinId(outPin), ed::PinKind::Output); ImGui::TextUnformatted("o"); ed::EndPin();
        ed::EndNode();
        if (out) ed::PopStyleColor(2);

        // Link colour: landed = green, else red for 'out', neutral for 'in'.
        // Connect the pins that face the centre so links stay clean.
        ImVec4 col = w.done ? ImVec4(0.40f, 0.85f, 0.40f, 1.0f)
                   : out    ? ImVec4(0.85f, 0.40f, 0.40f, 1.0f)
                            : ImVec4(0.60f, 0.62f, 0.72f, 1.0f);
        const int linkId = 5000 + i;
        if (out) ed::Link(ed::LinkId(linkId), ed::PinId(centerOut), ed::PinId(inPin),    col, 2.0f);
        else     ed::Link(ed::LinkId(linkId), ed::PinId(outPin),    ed::PinId(centerIn), col, 2.0f);
    }

    // Navigation must run while the editor is current (inside Begin/End).
    if (zoomToFit) { ed::NavigateToContent(); zoomToFit = false; }

    ed::End();
    ed::SetCurrentEditor(nullptr);

    if (doLayout) g_graphLaidOutFor = part.id;
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------
static void glfwError(int e, const char* d) { std::fprintf(stderr, "GLFW %d: %s\n", e, d); }

int main(int, char**) {
    std::string err;
    if (!g_db.open("panelwire.db", err) ||
        !g_db.initSchema(err) ||
        !g_db.seedIfEmpty(err)) {
        std::fprintf(stderr, "Database error: %s\n", err.c_str());
        return 1;
    }
    try { g_textScale = std::stof(g_db.getSetting("text_scale", "1.3")); } catch (...) {}
    refreshParts();
    refreshView();

    glfwSetErrorCallback(glfwError);
    if (!glfwInit()) return 1;
    const char* glsl_version = "#version 130";
    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 0);
    GLFWwindow* window = glfwCreateWindow(1200, 760,
        "Panel Wire Viewer - CNC Control Panel", nullptr, nullptr);
    if (!window) { glfwTerminate(); return 1; }
    glfwMakeContextCurrent(window);
    glfwSwapInterval(1);

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = "panelwire_ui.ini";
    ImGui::StyleColorsDark();
    ImGui_ImplGlfw_InitForOpenGL(window, true);
    ImGui_ImplOpenGL3_Init(glsl_version);

    g_edConfig.SettingsFile = "panelwire_nodes.json";
    g_edCtx = ed::CreateEditor(&g_edConfig);

    while (!glfwWindowShouldClose(window)) {
        glfwPollEvents();
        ImGui_ImplOpenGL3_NewFrame();
        ImGui_ImplGlfw_NewFrame();
        ImGui::NewFrame();

        // One full-window panel.
        const ImGuiViewport* vp = ImGui::GetMainViewport();
        ImGui::SetNextWindowPos(vp->WorkPos);
        ImGui::SetNextWindowSize(vp->WorkSize);
        ImGui::Begin("main", nullptr,
            ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
            ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse |
            ImGuiWindowFlags_MenuBar | ImGuiWindowFlags_NoBringToFrontOnFocus);

        if (ImGui::BeginMenuBar()) {
            if (ImGui::BeginMenu("File")) {
                if (ImGui::MenuItem("Reload from DB")) { refreshParts(); refreshView(); g_status = "Reloaded."; }
                if (ImGui::MenuItem("Reset DB to spec defaults")) ImGui::OpenPopup("reset_db");
                if (ImGui::MenuItem("Quit")) glfwSetWindowShouldClose(window, 1);
                ImGui::EndMenu();
            }
            ImGui::EndMenuBar();
        }
        // Reset confirm (opened from the menu above; popup lives at window scope).
        if (ImGui::BeginPopup("reset_db")) {
            ImGui::TextUnformatted("Discard ALL edits and reload the spec defaults?");
            if (ImGui::Button("Yes, reset")) {
                std::string e;
                if (g_db.resetAndReseed(e)) { refreshParts(); refreshView(); g_status = "Database reset to spec defaults."; }
                else g_status = "Reset failed: " + e;
                ImGui::CloseCurrentPopup();
            }
            ImGui::SameLine();
            if (ImGui::Button("Cancel")) ImGui::CloseCurrentPopup();
            ImGui::EndPopup();
        }

        if (ImGui::BeginTabBar("tabs")) {
            if (ImGui::BeginTabItem("Viewer")) { drawViewer(); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Build Progress")) { drawProgress(); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Landings")) { drawLandings(); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Graph")) { drawGraph(); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Wires (CRUD)")) { drawWiresCrud(); ImGui::EndTabItem(); }
            if (ImGui::BeginTabItem("Parts (CRUD)")) { drawPartsCrud(); ImGui::EndTabItem(); }
            ImGui::EndTabBar();
        }

        if (!g_status.empty()) {
            ImGui::Separator();
            ImGui::TextDisabled("%s", g_status.c_str());
        }
        ImGui::End();

        ImGui::Render();
        int w, h; glfwGetFramebufferSize(window, &w, &h);
        glViewport(0, 0, w, h);
        glClearColor(0.10f, 0.10f, 0.12f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);
        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
        glfwSwapBuffers(window);
    }

    ed::DestroyEditor(g_edCtx);
    ImGui_ImplOpenGL3_Shutdown();
    ImGui_ImplGlfw_Shutdown();
    ImGui::DestroyContext();
    glfwDestroyWindow(window);
    glfwTerminate();
    g_db.close();
    return 0;
}

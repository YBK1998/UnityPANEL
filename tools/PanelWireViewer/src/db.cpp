// ===========================================================================
// db.cpp  --  SQLite data access implementation.
// ===========================================================================
#include "db.h"
#include "seed_data.h"

#include <sqlite3.h>
#include <cstring>

// ----- small helpers -------------------------------------------------------
namespace {

std::string colText(sqlite3_stmt* st, int i) {
    const unsigned char* t = sqlite3_column_text(st, i);
    return t ? reinterpret_cast<const char*>(t) : std::string();
}

void bindText(sqlite3_stmt* st, int i, const std::string& s) {
    sqlite3_bind_text(st, i, s.c_str(), -1, SQLITE_TRANSIENT);
}

// Split ';'-separated token list, trimming whitespace and dropping empties.
std::vector<std::string> splitTokens(const std::string& s) {
    std::vector<std::string> out;
    size_t start = 0;
    while (start <= s.size()) {
        size_t end = s.find(';', start);
        if (end == std::string::npos) end = s.size();
        std::string tok = s.substr(start, end - start);
        size_t a = tok.find_first_not_of(" \t");
        size_t b = tok.find_last_not_of(" \t");
        if (a != std::string::npos) out.push_back(tok.substr(a, b - a + 1));
        start = end + 1;
    }
    return out;
}

} // namespace

// ----- open / close ---------------------------------------------------------
bool Db::open(const std::string& path, std::string& err) {
    if (sqlite3_open(path.c_str(), &db_) != SQLITE_OK) {
        err = db_ ? sqlite3_errmsg(db_) : "cannot open database";
        return false;
    }
    sqlite3_exec(db_, "PRAGMA foreign_keys=ON;", nullptr, nullptr, nullptr);
    return true;
}

void Db::close() {
    if (db_) { sqlite3_close(db_); db_ = nullptr; }
}

bool Db::exec(const char* sql, std::string& err) {
    char* msg = nullptr;
    if (sqlite3_exec(db_, sql, nullptr, nullptr, &msg) != SQLITE_OK) {
        err = msg ? msg : "sql error";
        sqlite3_free(msg);
        return false;
    }
    return true;
}

// ----- schema ---------------------------------------------------------------
bool Db::initSchema(std::string& err) {
    const char* sql =
        "CREATE TABLE IF NOT EXISTS parts ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  name TEXT NOT NULL,"
        "  category TEXT,"
        "  location TEXT,"
        "  spec TEXT,"
        "  description TEXT,"
        "  match TEXT"
        ");"
        "CREATE TABLE IF NOT EXISTS wires ("
        "  id INTEGER PRIMARY KEY AUTOINCREMENT,"
        "  number TEXT,"
        "  from_text TEXT,"
        "  to_text TEXT,"
        "  signal TEXT,"
        "  wire_spec TEXT,"
        "  area TEXT,"
        "  notes TEXT,"
        "  done INTEGER NOT NULL DEFAULT 0"
        ");"
        "CREATE TABLE IF NOT EXISTS settings ("
        "  key TEXT PRIMARY KEY,"
        "  value TEXT"
        ");";
    if (!exec(sql, err)) return false;

    // Migrate databases created before the 'done' column existed.
    // ALTER fails harmlessly with "duplicate column" if it is already there.
    sqlite3_exec(db_, "ALTER TABLE wires ADD COLUMN done INTEGER NOT NULL DEFAULT 0;",
                 nullptr, nullptr, nullptr);
    return true;
}

int Db::countParts() {
    sqlite3_stmt* st = nullptr;
    int n = 0;
    if (sqlite3_prepare_v2(db_, "SELECT COUNT(*) FROM parts;", -1, &st, nullptr) == SQLITE_OK) {
        if (sqlite3_step(st) == SQLITE_ROW) n = sqlite3_column_int(st, 0);
    }
    sqlite3_finalize(st);
    return n;
}

int Db::countWires() {
    sqlite3_stmt* st = nullptr;
    int n = 0;
    if (sqlite3_prepare_v2(db_, "SELECT COUNT(*) FROM wires;", -1, &st, nullptr) == SQLITE_OK) {
        if (sqlite3_step(st) == SQLITE_ROW) n = sqlite3_column_int(st, 0);
    }
    sqlite3_finalize(st);
    return n;
}

bool Db::seed(std::string& err) {
    if (!exec("BEGIN;", err)) return false;

    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "INSERT INTO parts(name,category,location,spec,description,match) "
        "VALUES(?,?,?,?,?,?);", -1, &st, nullptr);
    for (const auto& p : kSeedParts) {
        sqlite3_reset(st);
        bindText(st, 1, p.name);
        bindText(st, 2, p.category);
        bindText(st, 3, p.location);
        bindText(st, 4, p.spec);
        bindText(st, 5, p.description);
        bindText(st, 6, p.match);
        if (sqlite3_step(st) != SQLITE_DONE) { err = sqlite3_errmsg(db_); sqlite3_finalize(st); exec("ROLLBACK;", err); return false; }
    }
    sqlite3_finalize(st);

    st = nullptr;
    sqlite3_prepare_v2(db_,
        "INSERT INTO wires(number,from_text,to_text,signal,wire_spec,area,notes) "
        "VALUES(?,?,?,?,?,?,'');", -1, &st, nullptr);
    for (const auto& w : kSeedWires) {
        sqlite3_reset(st);
        bindText(st, 1, w.number);
        bindText(st, 2, w.from_text);
        bindText(st, 3, w.to_text);
        bindText(st, 4, w.signal);
        bindText(st, 5, w.wire_spec);
        bindText(st, 6, w.area);
        if (sqlite3_step(st) != SQLITE_DONE) { err = sqlite3_errmsg(db_); sqlite3_finalize(st); exec("ROLLBACK;", err); return false; }
    }
    sqlite3_finalize(st);

    return exec("COMMIT;", err);
}

bool Db::seedIfEmpty(std::string& err) {
    if (countParts() == 0 && countWires() == 0) return seed(err);
    return true;
}

bool Db::resetAndReseed(std::string& err) {
    if (!exec("DELETE FROM parts; DELETE FROM wires;"
              "DELETE FROM sqlite_sequence WHERE name IN('parts','wires');", err))
        return false;
    return seed(err);
}

// ----- queries ---------------------------------------------------------------
std::vector<Part> Db::parts(const std::string& search) {
    std::vector<Part> out;
    sqlite3_stmt* st = nullptr;
    std::string sql =
        "SELECT id,name,category,location,spec,description,match FROM parts ";
    if (!search.empty())
        sql += "WHERE name LIKE ?1 OR category LIKE ?1 OR location LIKE ?1 ";
    sql += "ORDER BY location, category, name;";
    if (sqlite3_prepare_v2(db_, sql.c_str(), -1, &st, nullptr) != SQLITE_OK) return out;
    if (!search.empty()) {
        std::string like = "%" + search + "%";
        bindText(st, 1, like);
    }
    while (sqlite3_step(st) == SQLITE_ROW) {
        Part p;
        p.id          = sqlite3_column_int(st, 0);
        p.name        = colText(st, 1);
        p.category    = colText(st, 2);
        p.location    = colText(st, 3);
        p.spec        = colText(st, 4);
        p.description = colText(st, 5);
        p.match       = colText(st, 6);
        out.push_back(std::move(p));
    }
    sqlite3_finalize(st);
    return out;
}

static Wire readWire(sqlite3_stmt* st) {
    Wire w;
    w.id        = sqlite3_column_int(st, 0);
    w.number    = colText(st, 1);
    w.from_text = colText(st, 2);
    w.to_text   = colText(st, 3);
    w.signal    = colText(st, 4);
    w.wire_spec = colText(st, 5);
    w.area      = colText(st, 6);
    w.notes     = colText(st, 7);
    w.done      = sqlite3_column_int(st, 8) != 0;
    return w;
}

std::vector<Wire> Db::wires(const std::string& search) {
    std::vector<Wire> out;
    sqlite3_stmt* st = nullptr;
    std::string sql =
        "SELECT id,number,from_text,to_text,signal,wire_spec,area,notes,done FROM wires ";
    if (!search.empty())
        sql += "WHERE number LIKE ?1 OR from_text LIKE ?1 OR to_text LIKE ?1 "
               "OR signal LIKE ?1 OR wire_spec LIKE ?1 OR area LIKE ?1 ";
    sql += "ORDER BY number;";
    if (sqlite3_prepare_v2(db_, sql.c_str(), -1, &st, nullptr) != SQLITE_OK) return out;
    if (!search.empty()) {
        std::string like = "%" + search + "%";
        bindText(st, 1, like);
    }
    while (sqlite3_step(st) == SQLITE_ROW) out.push_back(readWire(st));
    sqlite3_finalize(st);
    return out;
}

std::vector<Wire> Db::wiresForPart(const Part& p) {
    std::vector<Wire> out;
    std::vector<std::string> toks = splitTokens(p.match);
    if (toks.empty()) return out;

    std::string sql =
        "SELECT id,number,from_text,to_text,signal,wire_spec,area,notes,done FROM wires WHERE ";
    for (size_t i = 0; i < toks.size(); ++i) {
        if (i) sql += " OR ";
        sql += "(from_text LIKE ? OR to_text LIKE ?)";
    }
    sql += " ORDER BY number;";

    sqlite3_stmt* st = nullptr;
    if (sqlite3_prepare_v2(db_, sql.c_str(), -1, &st, nullptr) != SQLITE_OK) return out;
    int idx = 1;
    for (const auto& t : toks) {
        std::string like = "%" + t + "%";
        bindText(st, idx++, like);
        bindText(st, idx++, like);
    }
    while (sqlite3_step(st) == SQLITE_ROW) out.push_back(readWire(st));
    sqlite3_finalize(st);
    return out;
}

// ----- CRUD parts -----------------------------------------------------------
bool Db::addPart(const Part& p, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "INSERT INTO parts(name,category,location,spec,description,match) "
        "VALUES(?,?,?,?,?,?);", -1, &st, nullptr);
    bindText(st, 1, p.name);  bindText(st, 2, p.category);
    bindText(st, 3, p.location); bindText(st, 4, p.spec);
    bindText(st, 5, p.description); bindText(st, 6, p.match);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

bool Db::updatePart(const Part& p, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "UPDATE parts SET name=?,category=?,location=?,spec=?,description=?,match=? "
        "WHERE id=?;", -1, &st, nullptr);
    bindText(st, 1, p.name);  bindText(st, 2, p.category);
    bindText(st, 3, p.location); bindText(st, 4, p.spec);
    bindText(st, 5, p.description); bindText(st, 6, p.match);
    sqlite3_bind_int(st, 7, p.id);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

bool Db::deletePart(int id, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_, "DELETE FROM parts WHERE id=?;", -1, &st, nullptr);
    sqlite3_bind_int(st, 1, id);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

// ----- CRUD wires -----------------------------------------------------------
bool Db::addWire(const Wire& w, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "INSERT INTO wires(number,from_text,to_text,signal,wire_spec,area,notes,done) "
        "VALUES(?,?,?,?,?,?,?,?);", -1, &st, nullptr);
    bindText(st, 1, w.number); bindText(st, 2, w.from_text);
    bindText(st, 3, w.to_text); bindText(st, 4, w.signal);
    bindText(st, 5, w.wire_spec); bindText(st, 6, w.area);
    bindText(st, 7, w.notes);
    sqlite3_bind_int(st, 8, w.done ? 1 : 0);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

bool Db::updateWire(const Wire& w, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "UPDATE wires SET number=?,from_text=?,to_text=?,signal=?,wire_spec=?,"
        "area=?,notes=?,done=? WHERE id=?;", -1, &st, nullptr);
    bindText(st, 1, w.number); bindText(st, 2, w.from_text);
    bindText(st, 3, w.to_text); bindText(st, 4, w.signal);
    bindText(st, 5, w.wire_spec); bindText(st, 6, w.area);
    bindText(st, 7, w.notes);
    sqlite3_bind_int(st, 8, w.done ? 1 : 0);
    sqlite3_bind_int(st, 9, w.id);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

bool Db::setWireDone(int id, bool done, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_, "UPDATE wires SET done=? WHERE id=?;", -1, &st, nullptr);
    sqlite3_bind_int(st, 1, done ? 1 : 0);
    sqlite3_bind_int(st, 2, id);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

bool Db::deleteWire(int id, std::string& err) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_, "DELETE FROM wires WHERE id=?;", -1, &st, nullptr);
    sqlite3_bind_int(st, 1, id);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok) err = sqlite3_errmsg(db_);
    sqlite3_finalize(st);
    return ok;
}

// ----- settings -------------------------------------------------------------
std::string Db::getSetting(const std::string& key, const std::string& def) {
    sqlite3_stmt* st = nullptr;
    std::string val = def;
    sqlite3_prepare_v2(db_, "SELECT value FROM settings WHERE key=?;", -1, &st, nullptr);
    bindText(st, 1, key);
    if (sqlite3_step(st) == SQLITE_ROW) val = colText(st, 0);
    sqlite3_finalize(st);
    return val;
}

void Db::setSetting(const std::string& key, const std::string& value) {
    sqlite3_stmt* st = nullptr;
    sqlite3_prepare_v2(db_,
        "INSERT INTO settings(key,value) VALUES(?,?) "
        "ON CONFLICT(key) DO UPDATE SET value=excluded.value;", -1, &st, nullptr);
    bindText(st, 1, key);
    bindText(st, 2, value);
    sqlite3_step(st);
    sqlite3_finalize(st);
}

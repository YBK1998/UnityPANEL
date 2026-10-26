// ===========================================================================
// db.h  --  SQLite data access for PanelWireViewer.
// ===========================================================================
#pragma once
#include <string>
#include <vector>

struct sqlite3;

struct Part {
    int id = 0;
    std::string name;
    std::string category;
    std::string location;
    std::string spec;
    std::string description;
    std::string match;     // ';'-separated substring tokens
};

struct Wire {
    int id = 0;
    std::string number;
    std::string from_text;
    std::string to_text;
    std::string signal;     // used as the connection's short description
    std::string wire_spec;
    std::string area;
    std::string notes;
    bool done = false;      // physically landed / installed
};

class Db {
public:
    bool open(const std::string& path, std::string& err);
    void close();

    // Schema + seeding (seed only runs when the tables are empty).
    bool initSchema(std::string& err);
    bool seedIfEmpty(std::string& err);
    bool resetAndReseed(std::string& err);   // drop content, re-apply seed

    int countParts();
    int countWires();

    // Queries
    std::vector<Part> parts(const std::string& search = "");
    std::vector<Wire> wires(const std::string& search = "");
    std::vector<Wire> wiresForPart(const Part& p);

    // CRUD - parts
    bool addPart(const Part& p, std::string& err);
    bool updatePart(const Part& p, std::string& err);
    bool deletePart(int id, std::string& err);

    // CRUD - wires
    bool addWire(const Wire& w, std::string& err);
    bool updateWire(const Wire& w, std::string& err);
    bool deleteWire(int id, std::string& err);
    bool setWireDone(int id, bool done, std::string& err);   // quick toggle

    // Settings (key/value)
    std::string getSetting(const std::string& key, const std::string& def);
    void setSetting(const std::string& key, const std::string& value);

private:
    sqlite3* db_ = nullptr;
    bool exec(const char* sql, std::string& err);
    bool seed(std::string& err);
};

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Sprints (
    Id          TEXT    PRIMARY KEY,
    Status      TEXT    NOT NULL DEFAULT 'active'
                CHECK (Status IN ('active','closed')),
    Phase       TEXT    NOT NULL DEFAULT 'planning'
                CHECK (Phase IN ('planning','executing','evaluating','complete','failed')),
    StartedAt   TEXT    NOT NULL,
    ClosedAt    TEXT    NULL,
    CHECK (Id GLOB 'SPRINT-[0-9][0-9]*')
);

CREATE TABLE IF NOT EXISTS Tickets (
    Id              TEXT    PRIMARY KEY,
    Title           TEXT    NOT NULL,
    Description     TEXT    NOT NULL DEFAULT '',
    Status          TEXT    NOT NULL DEFAULT 'open'
                    CHECK (Status IN ('open','in_progress','closed','cancelled','archived')),
    Priority        TEXT    NOT NULL DEFAULT 'medium'
                    CHECK (Priority IN ('low','medium','high','critical')),
    Tier            TEXT    NOT NULL DEFAULT 'regular'
                    CHECK (Tier IN ('trivial','regular','complex')),
    SprintId        TEXT    NULL REFERENCES Sprints(Id) ON DELETE SET NULL,
    PlanApproach    TEXT    NOT NULL DEFAULT '',
    PlanFiles       TEXT    NOT NULL DEFAULT '',
    PlanApprovedAt  TEXT    NULL,
    Summary         TEXT    NOT NULL DEFAULT '',
    CreatedAt       TEXT    NOT NULL,
    UpdatedAt       TEXT    NOT NULL,
    CHECK (length(Id) >= 8 AND Id GLOB 'TKT-[0-9]*')
);
CREATE INDEX IF NOT EXISTS IX_Tickets_SprintId ON Tickets(SprintId);

CREATE TABLE IF NOT EXISTS AcceptanceCriteria (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    TicketId    TEXT    NOT NULL REFERENCES Tickets(Id) ON DELETE CASCADE,
    Ordinal     INTEGER NOT NULL,
    Text        TEXT    NOT NULL,
    Satisfied   INTEGER NOT NULL DEFAULT 0 CHECK (Satisfied IN (0,1)),
    CreatedAt   TEXT    NOT NULL,
    UNIQUE (TicketId, Ordinal)
);

CREATE TABLE IF NOT EXISTS Decisions (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    TicketId    TEXT    NOT NULL REFERENCES Tickets(Id) ON DELETE CASCADE,
    Title       TEXT    NOT NULL,
    Rationale   TEXT    NOT NULL DEFAULT '',
    CreatedAt   TEXT    NOT NULL,
    UNIQUE (TicketId, Title)
);

CREATE TABLE IF NOT EXISTS TestPlanItems (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    TicketId    TEXT    NOT NULL REFERENCES Tickets(Id) ON DELETE CASCADE,
    Ordinal     INTEGER NOT NULL,
    Description TEXT    NOT NULL,
    Expected    TEXT    NOT NULL DEFAULT '',
    Status      TEXT    NOT NULL DEFAULT 'pending'
                CHECK (Status IN ('pending','pass','fail','blocked')),
    UpdatedAt   TEXT    NOT NULL,
    UNIQUE (TicketId, Ordinal)
);

CREATE TABLE IF NOT EXISTS EvalReports (
    TicketId    TEXT    PRIMARY KEY REFERENCES Tickets(Id) ON DELETE CASCADE,
    RunId       TEXT    NOT NULL,
    Verdict     TEXT    NOT NULL CHECK (Verdict IN ('pass','fail','pending')),
    Content     TEXT    NOT NULL DEFAULT '',
    MatchedRunTs TEXT   NULL,
    CreatedAt   TEXT    NOT NULL,
    UpdatedAt   TEXT    NOT NULL,
    CHECK (length(RunId) >= 3)
);

CREATE TABLE IF NOT EXISTS SprintHandoffs (
    SprintId        TEXT    PRIMARY KEY REFERENCES Sprints(Id) ON DELETE CASCADE,
    CurrentFocus    TEXT    NOT NULL DEFAULT '',
    InProgress      TEXT    NOT NULL DEFAULT '',
    Discoveries     TEXT    NOT NULL DEFAULT '',
    NextSteps       TEXT    NOT NULL DEFAULT '',
    UpdatedAt       TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS ActiveTasks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    SprintId    TEXT    NOT NULL REFERENCES Sprints(Id) ON DELETE CASCADE,
    TaskRef     TEXT    NOT NULL,
    Ordinal     INTEGER NOT NULL,
    CreatedAt   TEXT    NOT NULL,
    UNIQUE (SprintId, Ordinal)
);

CREATE TABLE IF NOT EXISTS IdempotencyKeys (
    Key         TEXT    PRIMARY KEY,
    ResultJson  TEXT    NOT NULL,
    CreatedAt   TEXT    NOT NULL
);

// Member names mirror the RFC 854 Telnet command names.
// ReSharper disable InconsistentNaming

namespace MCEControl;

enum TelnetVerbs {
    WILL = 251,
    WONT = 252,
    DO = 253,
    DONT = 254,
    IAC = 255
}

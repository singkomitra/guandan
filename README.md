# guandan

## Development Setup

### Build Profiles

This project uses Unity 6 Build Profiles (**File -> Build Profiles**) to manage dev vs production builds. Two profiles are checked into the repo:

- **Development_Windows** - has `DEV_BUILD` defined. Start Game requires at least 1 player ready, useful for solo testing.
- **Production_Windows** - no `DEV_BUILD`. Start Game requires exactly 4 or 6 players, all ready.

**To switch profiles in the Editor** (affects Play mode): right-click the profile -> **Set as Active**. Keep `Development_Windows` active while working.

**To build**: select the desired profile in the Build Profiles window and click **Build**. No manual flag toggling needed.

### Connecting to a Game

| Scenario | Address | Port |
|---|---|---|
| Same machine | `127.0.0.1` | leave blank |
| Same local network | host's local IP (e.g. `192.168.x.x`) | leave blank |
| Outside network (port forward) | host's public IP | leave blank (7777 must be forwarded on the router as UDP) |
| Outside network (playit.gg) | playit.gg tunnel address | playit.gg tunnel port |

For outside network without router access, the host can use [playit.gg](https://playit.gg) - create a free account, run the agent, add a UDP tunnel on port 7777, and share the resulting address and port.

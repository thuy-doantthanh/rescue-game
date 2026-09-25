using SplashKitSDK;

namespace CooperativeRescue;

// Represents the current assistance status of a rescuee.
public enum RescueeState
{
    // Waiting for a responder to help.
    Waiting,

    // Walking behind a responder.
    Following,

    // Being carried by a responder.
    Carried,

    // Has reached the exit.
    Safe
}

// Captures one frame of player input: the movement direction and whether help was pressed.
public record PlayerAction(
    float DeltaX,
    float DeltaY,
    bool Help
);

// An immutable snapshot of a single rescuee's position and state.
public record PersonState(
    int Id,
    Point2D Position,
    bool Safe,
    RescueeState State,
    int? CarrierId
);
// The complete, read-only world state at one moment, sent from the host to the client.
public record Snapshot(
    Point2D Player1Position,
    Point2D Player2Position,
    IReadOnlyList<PersonState> People,
    float TimeLeft,
    int Player1Score,
    int Player2Score
);

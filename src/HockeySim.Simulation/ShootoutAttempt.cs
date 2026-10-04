using HockeySim.Domain;

namespace HockeySim.Simulation;

public sealed record ShootoutAttempt(TeamId TeamId, PlayerId ShooterId, PlayerId GoalieId, bool Scored);
/**
 * Shared TypeScript types — manually mirrored from BelterLife.Shared contracts.
 * Keep minimal; divergence risk mitigated by small surface area.
 * Code generation (NSwag) deferred to post-MVP.
 *
 * Timestamp conventions:
 *   REST responses: ISO 8601 UTC string
 *   SignalR game messages: Unix milliseconds (number)
 */

export interface PlayerDto {
  id: string;
  username: string;
}

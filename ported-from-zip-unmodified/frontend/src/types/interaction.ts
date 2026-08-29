// Interaction system types

export type InteractionType = "pickup" | "interact" | "open" | "talk";

export interface InteractableObject {
  id: string;
  name: string;
  interactionType: InteractionType;
  position: { x: number; y: number; z: number };
  promptText: string;
  isNearby: boolean;
}

export interface InteractionContext {
  nearbyObjects: InteractableObject[];
  activePrompt: InteractableObject | null;
}

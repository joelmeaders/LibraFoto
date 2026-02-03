import { SourceType, TransitionType, ImageFit } from './enums.model';

/**
 * Display settings data transfer object for the frontend.
 */
export interface DisplaySettingsDto {
  id: number;
  name: string;
  slideDuration: number;
  transition: TransitionType;
  transitionDuration: number;
  sourceType: SourceType;
  sourceId: number | null;
  shuffle: boolean;
  imageFit: ImageFit;
}

/**
 * Request to update display settings.
 */
export interface UpdateDisplaySettingsRequest {
  name?: string | null;
  slideDuration?: number | null;
  transition?: TransitionType | null;
  transitionDuration?: number | null;
  sourceType?: SourceType | null;
  sourceId?: number | null;
  shuffle?: boolean | null;
  imageFit?: ImageFit | null;
}

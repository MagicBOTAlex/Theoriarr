import ModelBase from 'App/ModelBase';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { CustomFormat } from 'Settings/CustomFormats/CustomFormats/useCustomFormats';
import MediaInfo from 'typings/MediaInfo';

export interface MovieFile extends ModelBase {
  movieId: number;
  relativePath: string;
  path: string;
  size: number;
  dateAdded: string;
  sceneName: string;
  releaseGroup: string;
  languages: Language[];
  quality: QualityModel;
  customFormats: CustomFormat[];
  customFormatScore: number;
  indexerFlags: number;
  mediaInfo: MediaInfo;
  qualityCutoffNotMet: boolean;
}

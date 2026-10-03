import ModelBase from 'App/ModelBase';
import { MovieImage } from 'Movies/Movie';

export type MovieCreditType = 'cast' | 'crew';

interface MovieCredit extends ModelBase {
  personTmdbId: number;
  personName: string;
  images: MovieImage[];
  type: MovieCreditType;
  department: string;
  job: string;
  character: string;
  order: number;
}

export default MovieCredit;

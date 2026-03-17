export interface Statistics {
    totalLinesTranslated: number
    totalFilesTranslated: number
    totalCharactersTranslated: number
    totalMovies: number
    totalEpisodes: number
    totalSubtitles: number
    translationsByMediaType: Record<string, number>
    translationsByService: Record<string, number>
    subtitlesByLanguage: Record<string, number>
    uniqueMoviesTranslated: number
    uniqueEpisodesTranslated: number
}

export interface DailyStatistic {
    date: string
    translationCount: number
}

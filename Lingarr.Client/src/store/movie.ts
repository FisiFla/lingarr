import { acceptHMRUpdate, defineStore } from 'pinia'
import { IFilter, IMovie, IPagedResult, IUseMovieStore, MediaType } from '@/ts'
import services from '@/services'

export const useMovieStore = defineStore('movie', {
    state: (): IUseMovieStore => ({
        movies: {
            totalCount: 0,
            pageSize: 0,
            pageNumber: 0,
            items: []
        },
        filter: {
            searchQuery: '',
            sortBy: 'Title',
            isAscending: true,
            pageNumber: 1
        },
        selectedMovies: [],
        selectAll: false
    }),
    getters: {
        getFilter: (state: IUseMovieStore): IFilter => state.filter,
        get: (state: IUseMovieStore): IPagedResult<IMovie> => state.movies
    },
    actions: {
        async setFilter(filterVal: IFilter) {
            this.filter = filterVal.searchQuery ? { ...filterVal, pageNumber: 1 } : filterVal
            await this.fetch()
        },
        async fetch() {
            this.movies = await services.media.movies(
                this.filter.pageNumber,
                this.filter.searchQuery,
                this.filter.sortBy,
                this.filter.isAscending
            )
            // Sort subtitles alphabetically for consistent display
            this.movies.items?.forEach((item) => {
                item.subtitles?.sort((a, b) => a.language.localeCompare(b.language))
            })
        },
        async exclude(type: MediaType, id: number) {
            await services.media.exclude(type, id)
        },
        async updateThreshold(type: MediaType, id: number, hours: string) {
            await services.media.threshold(type, id, hours)
        },
        clearSelection() {
            this.selectedMovies = []
            this.selectAll = false
        },
        toggleSelectAll() {
            if (this.selectAll) {
                this.selectedMovies = []
                this.selectAll = false
            } else {
                this.selectedMovies = [...this.movies.items]
                this.selectAll = true
            }
        },
        toggleSelect(movie: IMovie) {
            const index = this.selectedMovies.findIndex((m) => m.id === movie.id)
            if (index === -1) {
                this.selectedMovies.push(movie)
            } else {
                this.selectedMovies.splice(index, 1)
            }
            this.selectAll = this.selectedMovies.length === this.movies.items.length
        }
    }
})

if (import.meta.hot) {
    import.meta.hot.accept(acceptHMRUpdate(useMovieStore, import.meta.hot))
}

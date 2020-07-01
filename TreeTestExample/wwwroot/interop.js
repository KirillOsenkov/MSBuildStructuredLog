import Split from 'https://unpkg.com/split.js/dist/split.min.js'
function SplitView() {
    Split(['#one', '#two'], {
        sizes: [25, 75],
    })
}
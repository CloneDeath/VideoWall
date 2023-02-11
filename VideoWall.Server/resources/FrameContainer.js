export class FrameContainer {
    constructor(id) {
        this.id = id;
    }

    getElement() {
        return document.getElementById(this.id);
    }
}
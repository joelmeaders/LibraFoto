import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialogRef, MAT_DIALOG_DATA } from "@angular/material/dialog";
import { CreateTagDialogComponent } from "./create-tag-dialog.component";
import { TagDto } from "../../core/models";
import { TagService } from "../../core/services/tag.service";

describe("CreateTagDialogComponent", () => {
  it("creates a tag when form is valid", () => {
    const dialogRefStub = {
      close: vi.fn(),
    } as Partial<MatDialogRef<CreateTagDialogComponent>>;

    const mockTag: TagDto = {
      id: 1,
      name: "Family",
      color: "#ff0000",
      photoCount: 0,
    };

    const tagServiceStub = {
      createTag: vi.fn(() => of(mockTag)),
      updateTag: vi.fn(() => of(mockTag)),
    } as Partial<TagService>;

    TestBed.configureTestingModule({
      imports: [CreateTagDialogComponent],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefStub },
        { provide: TagService, useValue: tagServiceStub },
        { provide: MAT_DIALOG_DATA, useValue: null },
      ],
    });

    const fixture = TestBed.createComponent(CreateTagDialogComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.setValue({ name: "Family", color: "#ff0000" });
    component.save();

    expect(tagServiceStub.createTag).toHaveBeenCalled();
    expect(dialogRefStub.close).toHaveBeenCalledWith(true);
  });
});

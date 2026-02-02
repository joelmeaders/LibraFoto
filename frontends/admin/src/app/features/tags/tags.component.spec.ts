import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { describe, it, expect, vi } from "vitest";
import { MatDialog, MatDialogRef } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
import { TagsComponent } from "./tags.component";
import { TagService } from "../../core/services/tag.service";

describe("TagsComponent", () => {
  it("shows empty state when there are no tags", () => {
    const tagServiceStub = {
      getTags: vi.fn(() => of([])),
    } as Partial<TagService>;

    const dialogStub = {
      open: vi.fn(
        () => ({ afterClosed: () => of(false) }) as MatDialogRef<unknown>,
      ),
    } as unknown as MatDialog;

    const snackBarStub = {
      open: vi.fn(),
    } as Partial<MatSnackBar>;

    TestBed.configureTestingModule({
      imports: [TagsComponent],
      providers: [
        { provide: TagService, useValue: tagServiceStub },
        { provide: MatDialog, useValue: dialogStub },
        { provide: MatSnackBar, useValue: snackBarStub },
      ],
    });

    const fixture = TestBed.createComponent(TagsComponent);
    fixture.detectChanges();

    expect(tagServiceStub.getTags).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain("No tags yet");
  });
});

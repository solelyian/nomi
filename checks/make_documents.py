"""Generate ordinary Office/PDF files for both document-reader test suites.

pip install python-docx==1.2.0 python-pptx==1.0.2 reportlab==4.4.4 XlsxWriter==3.2.9
"""

from datetime import datetime
from pathlib import Path

import reportlab
from docx import Document
from PIL import Image
from pptx import Presentation
from pptx.util import Inches
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen.canvas import Canvas
from xlsxwriter import Workbook

FOLDER = Path(__file__).with_name("documents")


def word() -> None:
    document = Document()
    document.add_heading("Relevé d’avril", 0)
    paragraph = document.add_paragraph("Commandes : ")
    paragraph.add_run("120").bold = True
    paragraph.add_run("\tRetours : 4")
    document.add_paragraph("Remise & frais\n85,00 € HT")
    table = document.add_table(rows=1, cols=2)
    table.rows[0].cells[0].text = "Mai"
    table.rows[0].cells[1].text = "150"
    document.add_paragraph("Vérifier la marge avant envoi.")
    document.save(FOLDER / "releve.docx")


def spreadsheet() -> None:
    with Workbook(FOLDER / "budget.xlsx") as book:
        volumes = book.add_worksheet("Volumes")
        volumes.write_row(0, 0, ["Mois", "Commandes"])
        volumes.write_row(1, 0, ["Avril", 120])
        volumes.write_row(2, 0, ["Mai", 150])
        volumes.write("A4", "Total HT")
        volumes.write_formula("B4", "=SUM(B2:B3)", None, 270)
        conditions = book.add_worksheet("Conditions")
        conditions.write("A1", "Remise")
        conditions.write_number("B1", 0.15, book.add_format({"num_format": "0%"}))
        conditions.write_datetime(
            "C3", datetime(2026, 9, 14), book.add_format({"num_format": "yyyy-mm-dd"})
        )
        conditions.write("A4", "À payer")
        conditions.write_formula("B4", "=85*(1-B1)", None, 72.25)
        conditions.write("A5", "Solde")
        conditions.write_formula("B5", "=B4-B4", None, "")


def slides() -> None:
    deck = Presentation()
    for index in range(1, 13):
        slide = deck.slides.add_slide(deck.slide_layouts[6])
        frame = slide.shapes.add_textbox(
            Inches(1), Inches(1), Inches(8), Inches(4)
        ).text_frame
        paragraph = frame.paragraphs[0]
        paragraph.add_run().text = f"Étape {index} : "
        paragraph.add_run().text = "revue commerciale"
        if index == 1:
            frame.add_paragraph().text = "Avril : 120 commandes"
        if index == 2:
            frame.add_paragraph().text = "Mai : 150 commandes"
    deck.save(FOLDER / "revue.pptx")


def pdfs() -> None:
    font = Path(reportlab.__file__).parent / "fonts" / "Vera.ttf"
    pdfmetrics.registerFont(TTFont("Vera", str(font)))
    canvas = Canvas(str(FOLDER / "offre.pdf"), pageCompression=1)
    canvas.setFont("Vera", 14)
    canvas.drawString(72, 780, "Offre Nyne Technologies")
    canvas.drawString(72, 750, "Total : 85,00 € HT")
    canvas.drawString(72, 725, "Remise (15 %) appliquée")
    canvas.showPage()
    canvas.setFont("Vera", 12)
    canvas.drawString(72, 780, "Paiement sous 30 jours")
    canvas.drawString(72, 750, "Contact : ventes@nyne.example")
    canvas.save()
    for name in ["scan.pdf", "mixte.pdf"]:
        canvas = Canvas(str(FOLDER / name))
        if name == "mixte.pdf":
            canvas.drawString(72, 780, "Un texte selectionnable")
            canvas.showPage()
        canvas.drawInlineImage(Image.new("RGB", (160, 160), "white"), 72, 500)
        canvas.save()


if __name__ == "__main__":
    FOLDER.mkdir(exist_ok=True)
    word()
    spreadsheet()
    slides()
    pdfs()
    (FOLDER / "taux.csv").write_text(
        "mois;commandes;panier\navril;120;42,50\nmai;150;44,00\n", encoding="utf-8"
    )

import cgi
from google.appengine.ext import webapp
from google.appengine.ext.webapp.util import run_wsgi_app
from google.appengine.ext import db
import time
from sys import argv

class HtmlPage(): #TODO- Either go all the way with this structure or just drop it
    header = "<html><body>"
    instructions = """
        <p>This form generates virtual plants in a simulated plant community growing in the 3D virtual world, ScienceSim. This form is meant to be accessed from within the 3D simulation, and changes made here will not take effect until they are enabled there.<br></p>
        <hr>
        """
    footer = "</body></html>"


class MeadowRecordObject(db.Model):
    # Timestamp id for this record
    id = db.IntegerProperty()
    # CSV integers represeting the OpenMetaverse Tree types for each of the 5 species in the community
    plant_types = db.StringProperty()
    # CSV replacement probabilities for each species
    replacement_0 = db.StringProperty()
    replacement_1 = db.StringProperty()
    replacement_2 = db.StringProperty()
    replacement_3 = db.StringProperty()
    replacement_4 = db.StringProperty()
    replacement_5 = db.StringProperty()
    # X & Y dimensions of the matrix
    x_cells = db.IntegerProperty()
    y_cells = db.IntegerProperty()
    # X & Y location of the 0,0 cell (southwest corner) of the matrix
    x_position = db.FloatProperty()
    y_position = db.FloatProperty()
    # Spacing between cells
    spacing = db.FloatProperty()
    #Crop-like (1) or more natural (0) plant placement
    natural = db.IntegerProperty()
    # x*y character string with characters representing the plant_code at each position in the matrix (R for random and 0 for none)
    starting_matrix = db.TextProperty()


class ChooseMatrixSizePage(webapp.RequestHandler):
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(page.instructions)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/matrixform" method="post">
        <p><b>Matrix size:</b> Specify the size of the community matrix in x,y dimensions.<br>
        X size: <input type="text" name="x_size" value="10" maxlength="2" size="3"> &nbsp;&nbsp;
        Y size: <input type="text" name="y_size" value="10" maxlength="2" size="3"><i> (1 - 50)</i></p>
        <p><b>Matrix location:</b> Specify the region coordinates of the 0,0 (southwest) plant of the matrix.<br>
        X location: <input type="text" name="x_location" value="128" maxlength="5" size="5"> &nbsp;&nbsp;
        Y location: <input type="text" name="y_location" value="128" maxlength="5" size="5"><i> (0 - 256.0)</i></p>
        <p><b>Plant spacing:</b> Specify the distance between adjacent plants in the matrix.<br>
        Spacing: <input type="text" name="spacing" value="1" maxlength="4" size="4"><i> (0 - 20.0)</i></p>
        <p><b>Community appearance:</b> Specify whether the community should appear natural (plants randomly placed near the coordinates) or crop-like (plants placed exactly on the matrix coordinates).  This does not effect the simulation results - only the appearance.<br>
        Natural: <input name="natural" checked="checked" type="checkbox"></p>
        <input type="submit" value="Setup Matrix">
        </form>
        """

class SetupMatrixPage(webapp.RequestHandler):
    def post(self):
        self.x_size = self.request.get('x_size')
        self.y_size = self.request.get('y_size')
        self.x_location = self.request.get('x_location')
        self.y_location = self.request.get('y_location')
        self.spacing = self.request.get('spacing')
        self.natural = self.request.get('natural')
        page = HtmlPage()
        self.response.out.write(page.header)
        if (self.valid_inputs()):
            self.response.out.write(self.generate_form())
        else:
            self.response.out.write("Error - matrix size out of range!")
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/created" method="post">
        """

    plant_type_form = """
        <p><b>Plant types:</b><br>
        Specify the plant types to include in the community.</p>
        """

    plant_type_field = """
        <b></b>%s) <select name="plant_code_%s">
        <option value = "1">Pine1</option>
        <option value = "2">Pine2</option>
        <option value = "3">Pine3</option>
        <option value = "4">Pine4</option>
        <option value = "5">Oak</option>
        <option value = "6">Bush1</option>
        <option value = "7">Bush2</option>
        <option value = "8">Palm1</option>
        <option value = "9">Palm2</option>
        <option value = "10">Dogwood</option>
        <option value = "11">Cypress1</option>
        <option value = "12">Cypress2</option>
        <option value = "13">Plumeria</option>
        <option value = "14">Aspen</option>
        <option value = "15">Eucalyptus</option>
        <option value = "16">Fern</option>
        <option value = "17">Eelgrass</option>
        <option value = "18">SeaSword</option>
        <option value = "19">BeachGrass</option>
        <option value = "20">Kelp1</option>
        <option value = "21">Kelp2</option>
        </select>&nbsp;&nbsp;
        """

    replacement_matrix_form = """
        <p><b>Replacement Matrix</b><br>
        Specify the probability <i>(0-1)</i> that each plant type A will be replaced by each plant type B<br>
        when surrounded on all sides.</p>
        <table border="0"><tbody>
        <tr><td></td><td></td><td></td><td></td><td></td><th><b>A</b></th></tr>
        <tr><th></th><th></th><th>0 (gap)</th><th>1</th><th>2</th><th>3</th><th>4</th><th>5</th></tr>
        """

    community_matrix_form = """
    <p><b>Starting Community Matrix</b><br>
    Specify the initial plant type to place at each position of the community matrix.<br>
    <i>(R = randomly selected, 0 = No plant)</i></p>
    """

    community_matrix_field = """
        <select name="community_%s_%s">
        <option value = "R">R</option>
        <option value = "0">0</option>
        <option value = "1">1</option>
        <option value = "2">2</option>
        <option value = "3">3</option>
        <option value = "4">4</option>
        <option value = "5">5</option>
        </select>
        """

    def generate_form(self):
        page_width = int(self.y_size) * 47
        if (page_width < 750):
            page_width = 750
        assembled_form = """<div style="width:%s">""" % page_width
        assembled_form += self.form
        assembled_form += """<input type="submit" value="Submit">"""
        # Plant type form section
        assembled_form += self.plant_type_form
        for i in range(1,6):
            assembled_form += self.plant_type_field % (i, i)
        assembled_form += """</p>"""
        # Replacement matrix form section
        assembled_form += self.replacement_matrix_form
        for row in range(6):
            assembled_row = """<tr>"""
            if (row == 3):
                assembled_row += """<td><b> B &nbsp;&nbsp;&nbsp;</b></td><th> %s </th>""" % row
            else:
                assembled_row += """<td></td><th> %s </th>""" % row
            for  column in range(6):
                if (row == column):
                    assembled_row += """<td></td>"""
                else:
                    assembled_row += """<td><input type="text" name="replace_%s_%s" value="0.5" maxlength="4" size="4"></td>""" % (row, column)
            assembled_form += assembled_row + """</tr>"""
        assembled_form += """</table></p>"""
        # Community matrix form section
        assembled_form += self.community_matrix_form
        for x in range(int(self.x_size)):
            for y in range(int(self.y_size)):
                assembled_form += self.community_matrix_field % (x, y)
            assembled_form += """<br>"""
        # Pass along items from the first form page
        assembled_form += """
            <input type="hidden" name="x_size" value="%s">
            <input type="hidden" name="y_size" value="%s">
            <input type="hidden" name="x_location" value="%s">
            <input type="hidden" name="y_location" value="%s">
            <input type="hidden" name="spacing" value="%s">
            <input type="hidden" name="natural" value="%s">
            """ % (self.x_size, self.y_size, self.x_location, self.y_location, self.spacing, self.natural)
        assembled_form += """</p><input type="submit" value="Submit"></div></form>"""
        return assembled_form

    def valid_inputs(self):
        # Verify that the input values are valid and within limits
        x_dim = 0
        y_dim = 0
        x_loc = 0.0
        y_loc = 0.0
        spacing = 0.0
        try:
            x_dim = int(self.x_size)
            y_dim = int(self.y_size)
            x_loc = float(self.x_location)
            y_loc = float(self.y_location)
            spac = float(self.spacing)
        except:
            return False
        if ((x_dim < 1) or (x_dim > 50) or (y_dim < 1) or (y_dim > 50) or (x_loc < 0) or (x_loc > 256) or (y_loc < 0) or (y_loc > 256) or (spac < 0) or (spac > 20)):
            return False
        else:
            return True

class CreateCommunityRecord(webapp.RequestHandler):

    success_output = """
        <p><span style="font-size: larger;">The community is ready to load.</span></p>
        <p>To generate the community:</p>
        <p><ul><li>Move your avatar into the region where you would like it to load.</li><li>Paste the following text into the chat window:</li></ul></p>
        <p><blockquote style="font-size: larger;"><b>/18 %s</b></blockquote></p>
        """

    def post(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        if (self.valid_input()):
            self.id = int(time.time())
            self.store_record()
            self.response.out.write(self.success_output % self.id)
        else:
            self.response.out.write('Error - Invalid replacement values or no plant types specified!')
        self.response.out.write(page.footer)

    def valid_input(self):
        for x in range(6):
            for y in range(6):
                if (x != y):
                    test_value = self.request.get('replace_%s_%s' % (x, y))
                    try:
                        test_value = float(test_value)
                    except:
                        return False
                    if ((test_value < 0.0) or (test_value > 1.0)):
                        return False
        return True

    def store_record(self):
        # Get a db record instance to hold the form data
        record = MeadowRecordObject()
        # Store a timestamp as the record id
        record.id = self.id
        # Store the matrix xy sizes
        x_size = int(self.request.get('x_size'))
        y_size = int(self.request.get('y_size'))
        record.x_cells = x_size
        record.y_cells = y_size
        # Store the 0,0 xy location
        record.x_position = float(self.request.get('x_location'))
        record.y_position = float(self.request.get('y_location'))
        # Store the cell spacing and appearance style
        record.spacing = float(self.request.get('spacing'))
        appearance = self.request.get('natural')
        if (appearance == "on"):
            record.natural = 1
        else:
            record.natural = 0
        # Store the plant types
        record.plant_types = '%s,%s,%s,%s,%s' % (self.request.get('plant_code_1'), self.request.get('plant_code_2'), self.request.get('plant_code_3'), self.request.get('plant_code_4'), self.request.get('plant_code_5'))
        # Store the replacement probabilities
        replacement_strings = {}
        for x in range(6):
            row_string = ""
            for y in range(6):
                if (x != y):
                    row_string += self.request.get('replace_%s_%s' % (x, y))
                else:
                    row_string += '0'
                if (y < 5):
                    row_string += ','
            replacement_strings[str(x)] = row_string
        record.replacement_0 = replacement_strings['0']
        record.replacement_1 = replacement_strings['1']
        record.replacement_2 = replacement_strings['2']
        record.replacement_3 = replacement_strings['3']
        record.replacement_4 = replacement_strings['4']
        record.replacement_5 = replacement_strings['5']
        # Store the community matrix
        matrix_string = ""
        for x in range(x_size):
            for y in range(y_size):
                matrix_string += self.request.get('community_%s_%s' % (x, y))
        record.starting_matrix = matrix_string
        record.put()


class GetCommunityRecord(webapp.RequestHandler):
    def get(self):
        # Don't write any html header or footer info.  Just serve up the raw data.
        data = db.GqlQuery("SELECT * FROM MeadowRecordObject WHERE id=:1", int(self.request.get('id')))
        self.response.out.write("%s,%s,%s,%s,%s,%s<br>\n" % (data[0].x_cells, data[0].y_cells, data[0].x_position, data[0].y_position, data[0].spacing, data[0].natural))
        self.response.out.write(data[0].plant_types + '<br>\n')
        self.response.out.write(data[0].replacement_0 + '<br>\n')
        self.response.out.write(data[0].replacement_1 + '<br>\n')
        self.response.out.write(data[0].replacement_2 + '<br>\n')
        self.response.out.write(data[0].replacement_3 + '<br>\n')
        self.response.out.write(data[0].replacement_4 + '<br>\n')
        self.response.out.write(data[0].replacement_5 + '<br>\n')
        x_size = data[0].x_cells
        y_size = data[0].y_cells
        for x in range(x_size):
            output_line = ""
            for y in range(y_size):
                output_line += data[0].starting_matrix[x * y_size + y]
            self.response.out.write(output_line + '<br>\n')


application = webapp.WSGIApplication([('/', ChooseMatrixSizePage), ('/matrixform', SetupMatrixPage), ('/created', CreateCommunityRecord),('/data', GetCommunityRecord)], debug=True)

def main():
    run_wsgi_app(application)

if __name__ == "__main__":
    main()

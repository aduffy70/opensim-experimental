"""
 * Copyright (c) Contributors http://github.com/aduffy70/vMeadowGA
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the vMeadowGA module nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
"""

import cgi
from google.appengine.ext import webapp
from google.appengine.ext.webapp.util import run_wsgi_app
from google.appengine.ext import db
import time


class HtmlPage():
    """
    Class containing basic html page layout.  #TODO- Either go all the way with this structure or just drop it
    """
    header = '<html><body>'
    instructions = """
        <p>This form generates virtual plants in a simulated plant community growing in the 3D virtual world, ScienceSim. This form is meant to be accessed from within the 3D simulation, and changes made here will not take effect until they are enabled there.<br></p>
        <hr>
        """
    footer = '</body></html>'


class LogOrParametersPage(webapp.RequestHandler):
    """
    Page to select either the parameters form or log form.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.instructions)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    instructions = """
        <p>
            <b>Welcome to the virtual plant community simulations (vpcSim) web application.</b><br>
            From this page you can change the parameters controlling a virtual plant community or view data logged by the plant community.<br>
        </p>
        <hr>
        """

    form = """
        <form enctype="multipart/form-data" action="/parametersform1" method="get">
           <input type="submit" value="Change parameters" style="width: 175px">
        </form>
        <form enctype="multipart/form-data" action="/log" method="get">
            <input type="submit" value="View log data" style="width: 175px">
        </form>
        """

"""
START SECTION: Community parameters
"""


class MeadowRecordObject(db.Model):
    """
    Record class representing all the parameters to run a community simulation.
    """
    # Timestamp id for this record
    id = db.StringProperty()
    # CSV integers represeting the OpenMetaverse Tree types for each of the 5 species in the community
    plant_types = db.StringProperty()
    # CSV replacement probabilities for each species
    replacement_1 = db.StringProperty()
    replacement_2 = db.StringProperty()
    replacement_3 = db.StringProperty()
    replacement_4 = db.StringProperty()
    replacement_5 = db.StringProperty()
    #Lifespans for each species (S=short, M=mid, L=long)
    lifespans = db.StringProperty()
    #Altitude optimum for each species (L=low, M=mid, H=high)
    altitude_optimums = db.StringProperty()
    #Altitude effects for each species (N=None, L=Low, M=mid, H=high)
    altitude_effects = db.StringProperty()
    #Salinity optimum for each species (L=low, M=mid, H=high)
    salinity_optimums = db.StringProperty()
    #Salinity effects for each species (N=None, L=Low, M=mid, H=high)
    salinity_effects = db.StringProperty()
    #Drainage optimum for each species (L=low, M=mid, H=high)
    drainage_optimums = db.StringProperty()
    #Drainage effects for each species (N=None, L=Low, M=mid, H=high)
    drainage_effects = db.StringProperty()
    #Fertility optimum for each species (L=low, M=mid, H=high)
    fertility_optimums = db.StringProperty()
    #Fertility effects for each species (N=None, L=Low, M=mid, H=high)
    fertility_effects = db.StringProperty()
    # All other matrix info (disturbance_only, x_cells, y_cells, x_position, y_position, spacing, natural, terrain, salinity, drainage, and fertility maps)
    #Whether to only change the disturbance settings
    disturbance_only = db.IntegerProperty()
    #X,Y dimensions of the community matrix.
    #NOTE- currently locked at 50x50
    x_size = db.IntegerProperty()
    y_size = db.IntegerProperty()
    #Region coordinates of the southwest cell of the community matrix.
    #Note- currently locked at 5,5
    x_location = db.FloatProperty()
    y_location = db.FloatProperty()
    #Distance between community matrix cells
    #Note- currently locked a 5
    spacing = db.FloatProperty()
    #Whether to display a natural vs crop-like community
    natural = db.IntegerProperty()
    #Terrain, salinity, drainage, and fertility maps to use for the region
    terrain = db.IntegerProperty()
    salinity = db.IntegerProperty()
    drainage = db.IntegerProperty()
    fertility = db.IntegerProperty()
    #Matrix representing the starting values for each position in the matrix (R=random, N=disturbance, 0=gap, 1-5=plant types)
    starting_matrix = db.TextProperty()
    #Ongoing disturbance rate (random temporary disturbance each generation in addition to the permanent disturbance on the starting matrix) (N=none, L=low, M=mid, H=high)
    ongoing_disturbance = db.StringProperty()


class ParametersFormPageOne(webapp.RequestHandler):
    """
    First page of the two page community parameters form.  Accessed by the user by url or hyperlink.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(page.instructions)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    #Note- The matrix parameters are hidden for simplicity.  The matrix will always be 50*50 stretching acrossed an entire region.  The parameters are still there in case I decide to change back later.
    form = """
        <form enctype="multipart/form-data" action="/parametersform2" method="post">
            <p>
                <b>Modify disturbance settings only:</b> Check this box to only modify the disturbance settings.  Any other changes to the form will be ignored.<br>
                Disturbance only:<input name="disturbance_only" type="checkbox">
            </p>
            <input type="hidden" name="x_size" value="50" maxlength="2" size="3">
            <input type="hidden" name="y_size" value="50" maxlength="2" size="3">
            <input type="hidden" name="x_location" value="5" maxlength="5" size="5">
            <input type="hidden" name="y_location" value="5" maxlength="5" size="5">
            <input type="hidden" name="spacing" value="5" maxlength="4" size="4">
            <p>
                <b>Community appearance:</b> Specify whether the community should appear natural (plants randomly placed near the coordinates) or crop-like (plants placed exactly on the matrix coordinates).  This does not effect the simulation results - only the appearance.<br>
                Natural: <input name="natural" checked="checked" type="checkbox">
            </p>
            <p>
                <b>Terrain map:</b> Select the terrain<br>
                <input type="radio" name="terrain" value="0" checked>
                <img src="/images/FS23map.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Soil salinity map:</b> Select the pattern of soil salinity on the landscape<br>
                <input type="radio" name="salinity" value="0" checked>
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="salinity" value="1">
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="salinity" value="2">
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Soil drainage map:</b> Select the pattern of soil drainage on the landscape<br>
                <input type="radio" name="drainage" value="0">
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="drainage" value="1" checked>
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="drainage" value="2">
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Soil fertility map:</b> Select the pattern of soil fertility on the landscape<br>
                <input type="radio" name="fertility" value="0" checked>
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="fertility" value="1">
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="fertility" value="2" checked>
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <input type="submit" value="Setup Matrix">
        </form>
        """


class ParametersFormPageTwo(webapp.RequestHandler):
    """
    Second page of the two page community parameters form.  Accessed by the user by submitting the ChooseMatrixSize page.
    """
    def post(self):
        self.disturbance_only = self.request.get('disturbance_only')
        self.x_size = self.request.get('x_size')
        self.y_size = self.request.get('y_size')
        self.x_location = self.request.get('x_location')
        self.y_location = self.request.get('y_location')
        self.spacing = self.request.get('spacing')
        self.natural = self.request.get('natural')
        self.terrain = self.request.get('terrain')
        self.salinity = self.request.get('salinity')
        self.drainage = self.request.get('drainage')
        self.fertility = self.request.get('fertility')
        page = HtmlPage()
        self.response.out.write(page.header)
        if (self.valid_inputs()):
            self.response.out.write(self.generate_form())
        else:
            self.response.out.write('Error - matrix settings out of range!')
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/storeparameters" method="post">
        """

    plant_data_form = """
        <p>
            <b>Plant type %s: </b></br>&nbsp;&nbsp;
            <b>Appearance: </b>
            <select name="plant_code_%s">
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
            </select>
            <a href="/plants" target="_blank">View examples</a> <br>
            &nbsp;&nbsp;
            <b>Lifespan: </b>
            <select name="lifespan_%s">
                <option value = "S">Short</option>
                <option value = "M">Medium</option>
                <option value = "L">Long</option>
            </select><br>&nbsp;&nbsp;
            <b>Altitude- </b> Optimum:
            <select name="altitude_optimum_%s">
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select>&nbsp;&nbsp;&nbsp;&nbsp;
            Effect:
            <select name="altitude_effect_%s">
                <option value = "N">None</option>
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select><br>&nbsp;&nbsp;
            <b>Salinity- </b> Optimum:
            <select name="salinity_optimum_%s">
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select>&nbsp;&nbsp;&nbsp;&nbsp;
            Effect:
            <select name="salinity_effect_%s">
                <option value = "N">None</option>
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select><br>&nbsp;&nbsp;
            <b>Drainage- </b> Optimum:
            <select name="drainage_optimum_%s">
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select>&nbsp;&nbsp;&nbsp;&nbsp;
            Effect:
            <select name="drainage_effect_%s">
                <option value = "N">None</option>
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select><br>&nbsp;&nbsp;
            <b>Fertility- </b> Optimum:
            <select name="fertility_optimum_%s">
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select>&nbsp;&nbsp;&nbsp;&nbsp;
            Effect:
            <select name="fertility_effect_%s">
                <option value = "N">None</option>
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select><br>
        """

    replacement_matrix_form = """
        <p>
            <b>Replacement Matrix</b><br>
            Specify the probability <i>(0-1)</i> that each plant type A will be replaced by each plant type B<br>
            when surrounded on all sides.
        </p>
        <table border="0"><tbody>
            <tr>
                <td></td><td></td><td></td><td></td><td></td>
                <th><b>A</b></th>
            </tr>
            <tr>
                <th></th><th></th><th>0 (gap)</th><th>1</th>
                <th>2</th><th>3</th><th>4</th><th>5</th>
            </tr>
        """

    community_matrix_form = """
        <p>
            <b>Starting Community Matrix</b><br>
            Specify the initial plant type to place at each position of the community matrix.<br>
            <i>(R = randomly selected, N = Permanently disturbed area, 0 = Temporary gap)</i>
        </p>
        """

    community_matrix_field = """
        <select name="community_%s_%s">
            <option value = "R">R</option>
            <option value = "N">N</option>
            <option value = "0">0</option>
            <option value = "1">1</option>
            <option value = "2">2</option>
            <option value = "3">3</option>
            <option value = "4">4</option>
            <option value = "5">5</option>
        </select>
        """

    def generate_form(self):
        page_width = int(self.x_size) * 48
        if (page_width < 750):
            page_width = 750
        assembled_form = '<div style="width:%s">' % page_width
        assembled_form += self.form
        assembled_form += '<input type="submit" value="Submit">'
        # Plant type form section
        #assembled_form += self.plant_type_form
        for i in range(1,6):
            assembled_form += self.plant_data_form % (i, i, i, i, i, i, i, i, i, i, i)
        assembled_form += '</p>'
        # Replacement matrix form section
        assembled_form += self.replacement_matrix_form
        for row in range(1,6):
            assembled_row = '<tr>'
            if (row == 3):
                assembled_row += '<td><b> B &nbsp;&nbsp;&nbsp;</b></td><th> %s </th>' % row
            else:
                assembled_row += '<td></td><th> %s </th>' % row
            for  column in range(6):
                assembled_row += """
                    <td>
                        <select name="replace_%s_%s">
                            <option value = "L">Low</option>
                            <option value = "M">Mid</option>
                            <option value = "H">High</option>
                        </select>
                    </td>
                    """ % (row, column)
            assembled_form += assembled_row + '</tr>'
        assembled_form += '</tbody></table></p>'
        assembled_form += """
            <p>
                <b>Ongoing disturbance rate: <b>
                <select name="ongoing_disturbance">
                    <option value = "N">None</option>
                    <option value = "L">Low</option>
                    <option value = "M">Mid</option>
                    <option value = "H">High</option>
                    </select>
            """
        # Community matrix form section
        assembled_form += self.community_matrix_form
        for y in range(int(self.y_size) - 1, -1, -1):
            for x in range(int(self.x_size)):
                assembled_form += self.community_matrix_field % (x, y)
            assembled_form += """<br>"""
        # Pass along items from the first form page
        assembled_form += """
            <input type="hidden" name="disturbance_only" value="%s">
            <input type="hidden" name="x_size" value="%s">
            <input type="hidden" name="y_size" value="%s">
            <input type="hidden" name="x_location" value="%s">
            <input type="hidden" name="y_location" value="%s">
            <input type="hidden" name="spacing" value="%s">
            <input type="hidden" name="natural" value="%s">
            <input type="hidden" name="terrain" value="%s">
            <input type="hidden" name="salinity" value="%s">
            <input type="hidden" name="drainage" value="%s">
            <input type="hidden" name="fertility" value="%s">
            """ % (self.disturbance_only, self.x_size, self.y_size,
                   self.x_location, self.y_location, self.spacing,
                   self.natural, self.terrain, self.salinity, self.drainage,
                   self.fertility)
        assembled_form += '</p><input type="submit" value="Submit"></div></form>'
        return assembled_form

    def valid_inputs(self):
        #Verify that the input values are valid and within limits
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
        if ((x_dim < 1) or (x_dim > 50) or (y_dim < 1) or (y_dim > 50) or
            (x_loc < 0) or (x_loc > 256) or (y_loc < 0) or (y_loc > 256) or
            (spac < 0) or (spac > 20)):
            return False
        else:
            return True


class StoreParameters(webapp.RequestHandler):
    """
    Creates and stores a community record based on inputs from the 2 page
    community parameters form.  Accessed by submitting the SetupMatrix page form.
    """
    def post(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.id = str(int(time.time()))
        self.store_record()
        self.response.out.write(self.success_output % self.id)
        self.response.out.write(page.footer)

    success_output = """
        <p>
            <span style="font-size: larger;">
                The community is ready to load.
            </span>
        </p>
        <p>
            To generate the community:
        </p>
        <p>
            <ul>
                <li>Move your avatar into the region where you would like it to load.</li>
                <li>Paste the following text into the chat window:</li>
            </ul>
        </p>
        <p>
            <blockquote style="font-size: larger;"><b>/18 %s</b></blockquote>
        </p>
        """

    def store_record(self):
        # Get a db record instance to hold the form data
        record = MeadowRecordObject()
        # Store a timestamp as the record id
        record.id = self.id
        # Store the disturbance_only, matrix xy sizes, position, spacing, appearance, terrain, salinity, drainage, and fertility maps.
        disturbance_only = self.request.get('disturbance_only')
        if (disturbance_only == 'on'):
            record.disturbance_only = 1
        else:
            record.disturbance_only = 0
        record.x_size = int(self.request.get('x_size'))
        record.y_size = int(self.request.get('y_size'))
        record.x_location = float(self.request.get('x_location'))
        record.y_location = float(self.request.get('y_location'))
        record.spacing = float(self.request.get('spacing'))
        appearance = self.request.get('natural')
        if (appearance == 'on'):
            record.natural = 1
        else:
            record.natural = 0
        record.terrain = int(self.request.get('terrain'))
        record.salinity = int(self.request.get('salinity'))
        record.drainage = int(self.request.get('drainage'))
        record.fertility = int(self.request.get('fertility'))
        record.ongoing_disturbance = self.request.get('ongoing_disturbance')
        # Store the plant types
        record.plant_types = '%s,%s,%s,%s,%s' % (
            self.request.get('plant_code_1'), self.request.get('plant_code_2'),
            self.request.get('plant_code_3'), self.request.get('plant_code_4'),
            self.request.get('plant_code_5'))
        #Store the plant lifespans
        record.lifespans = '%s,%s,%s,%s,%s' % (self.request.get('lifespan_1'),
            self.request.get('lifespan_2'), self.request.get('lifespan_3'),
            self.request.get('lifespan_4'), self.request.get('lifespan_5'))
        #Store the environmental parameters
        record.altitude_optimums = '%s,%s,%s,%s,%s' % (
            self.request.get('altitude_optimum_1'),
            self.request.get('altitude_optimum_2'),
            self.request.get('altitude_optimum_3'),
            self.request.get('altitude_optimum_4'),
            self.request.get('altitude_optimum_5'))
        record.altitude_effects = '%s,%s,%s,%s,%s' % (
            self.request.get('altitude_effect_1'),
            self.request.get('altitude_effect_2'),
            self.request.get('altitude_effect_3'),
            self.request.get('altitude_effect_4'),
            self.request.get('altitude_effect_5'))
        record.salinity_optimums = '%s,%s,%s,%s,%s' % (
            self.request.get('salinity_optimum_1'),
            self.request.get('salinity_optimum_2'),
            self.request.get('salinity_optimum_3'),
            self.request.get('salinity_optimum_4'),
            self.request.get('salinity_optimum_5'))
        record.salinity_effects = '%s,%s,%s,%s,%s' % (
            self.request.get('salinity_effect_1'),
            self.request.get('salinity_effect_2'),
            self.request.get('salinity_effect_3'),
            self.request.get('salinity_effect_4'),
            self.request.get('salinity_effect_5'))
        record.drainage_optimums = '%s,%s,%s,%s,%s' % (
            self.request.get('drainage_optimum_1'),
            self.request.get('drainage_optimum_2'),
            self.request.get('drainage_optimum_3'),
            self.request.get('drainage_optimum_4'),
            self.request.get('drainage_optimum_5'))
        record.drainage_effects = '%s,%s,%s,%s,%s' % (
            self.request.get('drainage_effect_1'),
            self.request.get('drainage_effect_2'),
            self.request.get('drainage_effect_3'),
            self.request.get('drainage_effect_4'),
            self.request.get('drainage_effect_5'))
        record.fertility_optimums = '%s,%s,%s,%s,%s' % (
            self.request.get('fertility_optimum_1'),
            self.request.get('fertility_optimum_2'),
            self.request.get('fertility_optimum_3'),
            self.request.get('fertility_optimum_4'),
            self.request.get('fertility_optimum_5'))
        record.fertility_effects = '%s,%s,%s,%s,%s' % (
            self.request.get('fertility_effect_1'),
            self.request.get('fertility_effect_2'),
            self.request.get('fertility_effect_3'),
            self.request.get('fertility_effect_4'),
            self.request.get('fertility_effect_5'))
        # Store the replacement probabilities
        replacement_strings = {}
        for x in range(1,6):
            row_string = ''
            for y in range(6):
                row_string += self.request.get('replace_%s_%s' % (x, y))
                if (y < 5):
                    row_string += ','
            replacement_strings[str(x)] = row_string
        record.replacement_1 = replacement_strings['1']
        record.replacement_2 = replacement_strings['2']
        record.replacement_3 = replacement_strings['3']
        record.replacement_4 = replacement_strings['4']
        record.replacement_5 = replacement_strings['5']
        # Store the community matrix
        matrix_string = ''
        for y in range(record.y_size):
            for x in range(record.x_size):
                matrix_string += self.request.get('community_%s_%s' % (x, y))
        record.starting_matrix = matrix_string
        record.put()


class GetParameters(webapp.RequestHandler):
    """
    Returns the community record with a particular timestamp as XML.  Accessed by the vMeadow opensim module.
    """
    def get(self):
        data = db.GqlQuery("SELECT * FROM MeadowRecordObject WHERE id=:1",
                            self.request.get('id'))
        self.response.out.write(data[0].to_xml())


class PlantPicturesPage(webapp.RequestHandler):
    """
    Displays a page with photos of the different plant types in a new browser window.  Accessed through links on the SetupMatrix page form.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        picture_table = """
            <table border="0">
                <tbody>
                    <tr>
                        <th>Pine1</th><th>Pine2</th><th>Pine3</th>
                        <th>Pine4</th><th>Oak</th>
                    </tr>
                    <tr>
                        <td><img src="/images/Pine1.png" height="100" width="125"/></td>
                        <td><img src="/images/Pine2.png" height="100" width="125"/></td>
                        <td><img src="/images/Pine3.png" height="100" width="125"/></td>
                        <td><img src="/images/Pine4.png" height="100" width="125"/></td>
                        <td><img src="/images/Oak.png" height="100" width="125"/></td>
                    </tr>
                    <tr>
                        <td><br></td>
                    </tr>
                    <tr>
                        <th>Bush1</th><th>Bush2</th><th>Palm1</th>
                        <th>Palm2</th><th>Dogwood</th>
                    </tr>
                    <tr>
                        <td><img src="/images/Bush1.png" height="100" width="125"/></td>
                        <td><img src="/images/Bush2.png" height="100" width="125"/></td>
                        <td><img src="/images/Palm1.png" height="100" width="125"/></td>
                        <td><img src="/images/Palm2.png" height="100" width="125"/></td>
                        <td><img src="/images/Dogwood.png" height="100" width="125"/></td>
                    </tr>
                    <tr>
                        <td><br></td>
                    </tr>
                    <tr>
                        <th>Cypress1</th><th>Cypress2</th><th>Plumeria</th>
                        <th>Aspen</th><th>Eucalyptus</th>
                    </tr>
                    <tr>
                        <td><img src="/images/Cypress1.png" height="100" width="125"/></td>
                        <td><img src="/images/Cypress2.png" height="100" width="125"/></td>
                        <td><img src="/images/Plumeria.png" height="100" width="125"/></td>
                        <td><img src="/images/Aspen.png" height="100" width="125"/></td>
                        <td><img src="/images/Eucalyptus.png" height="100" width="125"/></td>
                    </tr>
                    <tr>
                        <td><br></td>
                    </tr>
                    <tr>
                        <th>Fern</th><th>Eelgrass</th><th>Seasword</th>
                        <th>Beachgrass</th><th>Kelp1</th>
                    </tr>
                    <tr>
                        <td><img src="/images/Fern.png" height="100" width="125"/></td>
                        <td><img src="/images/Eelgrass.png" height="100" width="125"/></td>
                        <td><img src="/images/Seasword.png" height="100" width="125"/></td>
                        <td><img src="/images/Beachgrass.png" height="100" width="125"/></td>
                        <td><img src="/images/Kelp1.png" height="100" width="125"/></td>
                    </tr>
                    <tr>
                        <td><br></td>
                    </tr>
                    <tr>
                        <th>Kelp2</th>
                    </tr>
                    <tr>
                        <td><img src="/images/Kelp2.png" height="100" width="125"/></td>
                    </tr>
                </tbody>
            </table>
            """
        self.response.out.write(picture_table)
        self.response.out.write(page.footer)


"""
END SECTION: Community parameters
"""


"""
START SECTION: Community logging.
"""


class SimulationLogObject(db.Model):
    """
    Record class representing the log output from a single step of a community simulation.  Includes a timestamp in case a single step was visualized multiple times and a region tag in case the same simulation id was used on more than one region.
    """
    # Time record was created
    time_stamp = db.DateTimeProperty(auto_now_add=True)
    # Simulation ID of the simulation that created this record
    sim_id = db.StringProperty()
    region_tag = db.StringProperty()
    # CSV string of simulation step and counts for each species
    data = db.StringProperty()


class AddLogRecord(webapp.RequestHandler):
    """
    Stores logged output from a community simulation.  Accessed by the vMeadow opensim region module.
    """
    def get(self):
        record = SimulationLogObject()
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        data = self.request.get('data')
        if ((len(sim_id) == 10) and (len(data.split(',')) == 7)):
            record.sim_id = sim_id
            record.region_tag = region_tag
            record.data = data
            record.put()
            self.response.out.write('SUCCESS')
        else:
            #Send some return value to tell the region that this failed
            self.response.out.write('FAILED')


class LogFormPage(webapp.RequestHandler):
    """
    A page to request log data by simulation id and region tag.  Accessed by the user by url or hyperlink.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/getlog" method="post">
            <p>
                <b>View log records for a simulation on a particular region: </b><br>
                Simulation ID: <input type="text" name="sim_id" maxlength="10" size="11"> &nbsp;&nbsp;&nbsp;&nbsp;
                Region tag: <input type="text" name="region_tag" maxlength="20" size="21"><br>
                <input type="submit" value="Get log records">
            </p>
        </form>
        """

class GetLogRecords(webapp.RequestHandler):
    """
    Retrieves the log data requested on the log page.  Accessed by submitting the LogFormPage.
    """
    def post(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        records = db.GqlQuery("SELECT * FROM SimulationLogObject WHERE sim_id=:1 AND region_tag=:2 ORDER BY time_stamp", sim_id, region_tag)
        if (records.count(1) > 0):
            self.response.out.write("<p><b>Records for " + sim_id + " in " + region_tag + ":</b></p>")
            self.response.out.write("<b>Simulation ID, Gap count, Species1 count, Species2 count, Species3 count, Species4 count, Species5 count</b><br>")
            for record in records:
                self.response.out.write(record.data + ',' +
                                        str(record.time_stamp) + '<br>')
        else:
            self.response.out.write('No records for ' + sim_id + " in " +
                                    region_tag)
        self.response.out.write(page.footer)


class DeleteLogRecords(webapp.RequestHandler):
    """
    Deletes all log records with a specific simulation id and region tag.  Accessed by the vMeadow opensim region module.
    """
    def get(self):
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        records = db.GqlQuery("SELECT * FROM SimulationLogObject WHERE sim_id=:1 AND region_tag=:2", sim_id, region_tag)
        if (records.count(1) > 0):
            for record in records:
                record.delete()
        self.response.out.write('SUCCESS')


"""
END SECTION: Community logging
"""


# url to class mapping
application = webapp.WSGIApplication([('/', LogOrParametersPage),
                                      ('/parametersform1', ParametersFormPageOne),
                                      ('/parametersform2', ParametersFormPageTwo),
                                      ('/storeparameters', StoreParameters),
                                      ('/data', GetParameters),
                                      ('/plants', PlantPicturesPage),
                                      ('/addlog', AddLogRecord),
                                      ('/log', LogFormPage),
                                      ('/getlog', GetLogRecords),
                                      ('/deletelog', DeleteLogRecords)], debug=True)

def main():
    run_wsgi_app(application)

if __name__ == "__main__":
    main()

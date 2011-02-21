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
        <p>
            This form generates virtual plants in a simulated plant community growing in the 3D virtual world, ScienceSim. Changes made here will not take effect until they are enabled there.<br>
        </p>
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
            <b>Welcome to the virtual plant community simulations (vpcSim) web application.</b>
        </p>
        From this page you can:
        <ul>
            <li>change the parameters controlling a virtual plant community,</li>
            <li>change only the disturbance pattern, or </li>
            <li>view data logged by the plant community.</li>
        </ul>
        <hr>
        """

    form = """
        <form enctype="multipart/form-data" action="/parametersform1" method="get">
           <input type="submit" value="Change all parameters" style="width: 175px">
        </form>
        <form enctype="multipart/form-data" action="/selectmap" method="get">
           <input type="submit" value="Change disturbance only" style="width: 175px">
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

    #Whether to only change the disturbance settings
    disturbance_only = db.IntegerProperty()

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

    #TODO- These values are no longer adjustable but they are expected by the region module so we still include them here and provide the locked values when we create a record.  Update the region module to not require them and remove them completely here.
    #XY dimensions of the community matrix
    #currently locked at 50x50
    x_size = db.IntegerProperty()
    y_size = db.IntegerProperty()
    #Region coordinates of the southwest cell of the community matrix.
    #currently locked at 5,5
    x_location = db.FloatProperty()
    y_location = db.FloatProperty()
    #Distance between community matrix cells
    #Note- currently locked a 5
    spacing = db.FloatProperty()


class ParametersFormPageOne(webapp.RequestHandler):
    """
    First page of the three page community parameters form.  Accessed by the user by url or hyperlink. Controls terrain and environment parameters (and includes some hidden matrix parameters).
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(page.instructions)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/parametersform2" method="post">
            <p>
                <b>Terrain map:</b> Select the terrain<br>0
                <input type="radio" name="terrain" value="0" checked>
                <img src="/images/Terrain0_map.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="terrain" value="1">1
                <img src="/images/Terrain1_map.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="terrain" value="2">2
                <img src="/images/Terrain2_map.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="terrain" value="3">3
                <img src="/images/Terrain3_map.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Community appearance:</b> Specify whether the community should appear natural (plants randomly placed near the coordinates) or crop-like (plants placed exactly on the matrix coordinates).  This does not effect the simulation results - only the appearance.<br>
                Natural: <input name="natural" checked="checked" type="checkbox">
            </p>
            <p>
                <b>Soil salinity map:</b> Select the pattern of soil salinity on the landscape<br>
                <input type="radio" name="salinity" value="0" checked>0
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="salinity" value="1">1
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="salinity" value="2">2
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Soil drainage map:</b> Select the pattern of soil drainage on the landscape<br>
                <input type="radio" name="drainage" value="0">0
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="drainage" value="1" checked>1
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="drainage" value="2">2
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <p>
                <b>Soil fertility map:</b> Select the pattern of soil fertility on the landscape<br>
                <input type="radio" name="fertility" value="0" checked>0
                <img src="/images/SoilXmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="fertility" value="1">1
                <img src="/images/SoilYmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
                <input type="radio" name="fertility" value="2" checked>2
                <img src="/images/SoilZmap.jpg" height="100" width="100" />
                &nbsp;&nbsp;
            </p>
            <input type="submit" value="Continue...">
        </form>
        """


class ParametersFormPageTwo(webapp.RequestHandler):
    """
    Second page of the three page community parameters form.  Accessed by the user by submitting ParametersFormPageOne.  Controls plant settings
    """
    def post(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.form_header)
        for i in range(1,6):
            self.response.out.write(self.form_plant_data % (i, i, i, i, i, i, i, i, i, i, i))
        self.response.out.write(self.form_replacement_matrix)
        for row in range(1,6):
            if (row == 3):
                self.response.out.write(self.form_response_table_row % row)
            else:
                self.response.out.write(self.form_response_table_header % row)
            for  column in range(6):
                self.response.out.write(self.form_replacement_value_select % (row, column))
            self.response.out.write('</tr>')
        self.response.out.write('</tbody></table></p>')
        self.response.out.write(self.form_hidden_fields % (self.request.get('natural'), self.request.get('terrain'), self.request.get('salinity'),
            self.request.get('drainage'), self.request.get('fertility')))
        self.response.out.write(self.form_submit_button)
        self.response.out.write(page.footer)

    form_header = """
        <form enctype="multipart/form-data" action="/parametersform3" method="post">
        """

    form_plant_data = """
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
            </select>
            <a href="/plants" target="_blank">View examples</a><br>&nbsp;&nbsp;
            <b>Lifespan: </b>
            <select name="lifespan_%s">
                <option value = "S">Short</option>
                <option selected value = "M">Medium</option>
                <option value = "L">Long</option>
            </select><br>&nbsp;&nbsp;
            <b>Altitude- </b> Optimum:
            <select name="altitude_optimum_%s">
                <option value = "L">Low</option>
                <option  selected value = "M">Mid</option>
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
                <option selected value = "M">Mid</option>
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
                <option selected value = "M">Mid</option>
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
                <option selected value = "M">Mid</option>
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

    form_replacement_matrix = """
        </p>
        <p>
            <b>Replacement Matrix</b><br>
            Specify the probability that an individual plant type A will be replaced by plant type B<br>
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

    form_response_table_row = '<tr><td><b> B &nbsp;&nbsp;&nbsp;</b></td><th> %s </th>'

    form_response_table_header = '<tr><td></td><th> %s </th>'

    form_replacement_value_select = """
        <td>
            <select name="replace_%s_%s">
                <option value = "L">Low</option>
                <option selected value = "M">Mid</option>
                <option value = "H">High</option>
            </select>
        </td>
        """

    form_hidden_fields = """
        <input type="hidden" name="disturbance_only" value="0">
        <input type="hidden" name="natural" value="%s">
        <input type="hidden" name="terrain" value="%s">
        <input type="hidden" name="salinity" value="%s">
        <input type="hidden" name="drainage" value="%s">
        <input type="hidden" name="fertility" value="%s">
        """

    form_submit_button = '</p><input type="submit" value="Continue..."></form>'


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
        self.response.out.write(self.picture_table)
        self.response.out.write(page.footer)

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
                    <th>Beachgrass</th><th></th>
                </tr>
                <tr>
                    <td><img src="/images/Fern.png" height="100" width="125"/></td>
                    <td><img src="/images/Eelgrass.png" height="100" width="125"/></td>
                    <td><img src="/images/Seasword.png" height="100" width="125"/></td>
                    <td><img src="/images/Beachgrass.png" height="100" width="125"/></td>
                    <td></td>
                </tr>
            </tbody>
        </table>
        """


class SelectTerrainMap(webapp.RequestHandler):
    """
    Select the map for the background on ParametersFormPageThree.  Accessed by selecting Change disturbance only from LogOrParameters.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.form_header)
        self.response.out.write(self.form_terrain_radio_buttons)
        self.response.out.write(self.form_hidden_fields)
        self.response.out.write(self.form_submit_button)

        self.response.out.write(self.form_footer)
        self.response.out.write(page.footer)

    form_header = """
        <form enctype="multipart/form-data" action="/parametersform3" method="post">
        """

    form_terrain_radio_buttons = """
        <p>
            <b>Select the terrain used in the region:</b><br>
            <input type="radio" name="terrain" value="0" checked>0
            <img src="/images/Terrain0_map.jpg" height="100" width="100" />
            &nbsp;&nbsp;
            <input type="radio" name="terrain" value="1">1
            <img src="/images/Terrain1_map.jpg" height="100" width="100" />
            &nbsp;&nbsp;
            <input type="radio" name="terrain" value="2">2
            <img src="/images/Terrain2_map.jpg" height="100" width="100" />
            &nbsp;&nbsp;
            <input type="radio" name="terrain" value="3">3
            <img src="/images/Terrain3_map.jpg" height="100" width="100" />
            &nbsp;&nbsp;
        </p>
        """

    form_hidden_fields = """
        <input type="hidden" name="disturbance_only" value="1">
        """

    form_submit_button = """
        <input type="submit" value="Continue...">
        """

    form_footer = '</form>'



class ParametersFormPageThree(webapp.RequestHandler):
    """
    Page 3 of the three page parameters request form.  Accessed by submitting ParametersFormPagetwo.  Controls starting matrix and disturbance settings and stores the output from all three pages.
    """
    def post(self):
        submit_value = self.request.get('submit_value')
        if (submit_value == 'Submit parameters'):
            page = HtmlPage()
            self.response.out.write(page.header)
            self.id = str(int(time.time()))
            self.store_record()
            self.response.out.write(self.success_output % self.id)
            self.response.out.write(page.footer)
        else:
            self.redraw_form(submit_value)

    def redraw_form(self, submit_value):
        page = HtmlPage()
        disturbance_only = self.request.get('disturbance_only')
        terrain = self.request.get('terrain')
        starting_matrix = list(self.request.get('starting_matrix'))
        if (len(starting_matrix) == 0):
            #Set up the default starting matrix with all Rs
            starting_matrix = []
            for i in range(2500):
                starting_matrix.append('R')
        clicked = self.request.get('clicked')
        submit_value = self.request.get('submit_value')
        selected = []
        selected_string = self.request.get('selected')
        if (selected_string != ''):
            selected = selected_string.split(',')
        if (submit_value == 'Apply cell value to cells'):
            cell_value = self.request.get('cell_value')
            for cell in selected:
                starting_matrix[int(cell)] = cell_value
            selected = []
        elif (submit_value == 'Apply cell value to area'):
            cell_value = self.request.get('cell_value')
            selected_area = self.get_area_list_from_corners(int(selected[0]), int(selected[1]))
            for cell in selected_area:
                starting_matrix[int(cell)] = cell_value
            selected = []
        elif (clicked in selected):
            selected.remove(clicked)
        else:
            selected.append(clicked)
        self.response.out.write(page.header)
        self.response.out.write(self.form_header)
        self.response.out.write(self.form_ongoing_disturbance_selector)
        self.response.out.write(self.form_starting_matrix_map_label)
        self.response.out.write(self.form_table_header % terrain)
        for j in range(50):
            for i in range(50):
                index = (j * 50) + i
                if (str(index) in selected):
                    image = 'selected'
                else:
                    image = starting_matrix[index]
                self.response.out.write(self.form_button % (index, image))
            if (j != 49):
                self.response.out.write('<br>')
        self.response.out.write(self.form_table_footer)
        if (len(selected) > 0):
            if (disturbance_only == "0"):
                self.response.out.write(self.form_cell_value_selector)
            else:
                self.response.out.write(self.form_cell_value_selector_disturbance_only)
            self.response.out.write(self.form_assign_cells_button)
            if (len(selected) == 2):
                self.response.out.write(self.form_assign_area_button)
        else:
            self.response.out.write('<br>')
        self.response.out.write(self.form_submit_button)
        #Pass the list of selected cells, the current starting matrix, whether we are only changing disturbance, ongoing disturbance value and which terrain we are using.
        self.response.out.write(self.form_active_hidden_fields % (
            ','.join(selected),
            ''.join(starting_matrix),
            disturbance_only,
            self.request.get('ongoing_disturbance'),
            terrain))
        #Pass the values from previous form pages (if we used those previous pages)
        if (disturbance_only == '0'):
            self.response.out.write(self.form_passive_hidden_fields % (
                self.request.get('natural'),
                self.request.get('salinity'),
                self.request.get('drainage'),
                self.request.get('fertility')))
            for x in range(1, 6):
                self.response.out.write(self.form_plant_code_hidden_field % (x, self.request.get('plant_code_%s' % x)))
                self.response.out.write(self.form_lifespan_hidden_field % (x, self.request.get('lifespan_%s' % x)))
                self.response.out.write(self.form_altitude_optimum_hidden_field % (x, self.request.get('altitude_optimum_%s' % x)))
                self.response.out.write(self.form_altitude_effect_hidden_field % (x, self.request.get('altitude_effect_%s' % x)))
                self.response.out.write(self.form_drainage_optimum_hidden_field % (x, self.request.get('drainage_optimum_%s' % x)))
                self.response.out.write(self.form_drainage_effect_hidden_field % (x, self.request.get('drainage_effect_%s' % x)))
                self.response.out.write(self.form_salinity_optimum_hidden_field % (x, self.request.get('salinity_optimum_%s' % x)))
                self.response.out.write(self.form_salinity_effect_hidden_field % (x, self.request.get('salinity_effect_%s' % x)))
                self.response.out.write(self.form_fertility_optimum_hidden_field % (x, self.request.get('fertility_optimum_%s' % x)))
                self.response.out.write(self.form_fertility_effect_hidden_field % (x, self.request.get('fertility_effect_%s' % x)))
                for y in range(6):
                    self.response.out.write(self.form_replace_hidden_field % (x, y, self.request.get('replace_%s_%s' % (x, y))))
        self.response.out.write(self.form_footer)
        self.response.out.write(page.footer)

    def get_area_list_from_corners(self, corner1, corner2):
        y1 = corner1 / 50
        x1 = corner1 % 50
        y2 = corner2 / 50
        x2 = corner2 % 50
        lower_x = x1
        upper_x = x2
        lower_y = y1
        upper_y = y2
        if (x1 >= x2):
            lower_x = x2
            upper_x = x1
        if (y1 >= y2):
            lower_y = y2
            upper_y = y1
        area_list = []
        for y in range(lower_y, upper_y + 1):
            for x in range(lower_x, upper_x + 1):
                area_list.append(y * 50 + x)
        return area_list

    def store_record(self):
        # Get a db record instance to hold the form data
        record = MeadowRecordObject()
        # Store a timestamp as the record id
        record.id = self.id
        # Store the disturbance_only, matrix xy sizes, position, spacing, appearance, terrain, salinity, drainage, and fertility maps.
        record.disturbance_only = int(self.request.get('disturbance_only'))
        if (record.disturbance_only == 0):
            record.x_size = 50
            record.y_size = 50
            record.x_location = 5.0
            record.y_location = 5.0
            record.spacing = 5.0
            appearance = self.request.get('natural')
            if (appearance == 'on'):
                record.natural = 1
            else:
                record.natural = 0
            record.terrain = int(self.request.get('terrain'))
            record.salinity = int(self.request.get('salinity'))
            record.drainage = int(self.request.get('drainage'))
            record.fertility = int(self.request.get('fertility'))
            record.plant_types = ''
            record.lifespans = ''
            record.altitude_optimums = ''
            record.altitude_effects = ''
            record.salinity_optimums = ''
            record.salinity_effects = ''
            record.drainage_optimums = ''
            record.drainage_effects = ''
            record.fertility_optimums = ''
            record.fertility_effects = ''
            for i in range(1, 6):
                comma = ','
                if (i == 5):
                    comma = ''
                record.plant_types += self.request.get('plant_code_%s' % i) + comma
                record.lifespans += self.request.get('lifespan_%s' % i) + comma
                record.altitude_optimums += self.request.get('altitude_optimum_%s' % i) + comma
                record.altitude_effects += self.request.get('altitude_effect_%s' % i) + comma
                record.salinity_optimums += self.request.get('salinity_optimum_%s' % i) + comma
                record.salinity_effects += self.request.get('salinity_effect_%s' % i) + comma
                record.drainage_optimums += self.request.get('drainage_optimum_%s' % i) + comma
                record.drainage_effects += self.request.get('drainage_effect_%s' % i) + comma
                record.fertility_optimums += self.request.get('fertility_optimum_%s' % i) + comma
                record.fertility_effects += self.request.get('fertility_effect_%s' % i) + comma
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
        record.ongoing_disturbance = self.request.get('ongoing_disturbance')
        # Store the community matrix
        #This matrix starts with 0 in the NW corner and I need 0 in the SW corner
        temp_starting_matrix = self.request.get('starting_matrix')
        upside_down_matrix = []
        for y in range(50):
            row = ''
            for x in range(50):
                row += temp_starting_matrix[y * 50 + x]
            upside_down_matrix.append(row)
        record.starting_matrix = ''
        for y in range(50):
            record.starting_matrix += upside_down_matrix[49 - y]
        record.put()

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
            <blockquote style="font-size: larger;">
                <b>/18 %s</b>
            </blockquote>
        </p>
        """

    form_header = '<form enctype="multipart/form-data" action="/parametersform3" method="post">'

    form_ongoing_disturbance_selector= """
        <p>
            <b>Ongoing disturbance rate: <b>
            <select name="ongoing_disturbance">
                <option value = "N">None</option>
                <option value = "L">Low</option>
                <option value = "M">Mid</option>
                <option value = "H">High</option>
            </select>
        <p>
        """

    form_starting_matrix_map_label = """
        <b>Click on the map to select one or more cells to set the starting status:</b>
        """

    form_table_header = '<table background="/images/Terrain%s_map.jpg"><tbody><td>'

    form_button = '<input type="image" name="clicked" value="%s" src="/images/%sbutton.png" style="width: 10px; height=10px;">'

    form_table_footer = '</td></tbody></table>'

    form_cell_value_selector_disturbance_only = """
        <b>Cell value:</b>
        <select name="cell_value">
            <option value = "R">Not disturbed</option>
            <option value = "N">Permanent disturbance</option>
        </select>
        """

    form_cell_value_selector = """
        <b>Cell value:</b>
        <select name="cell_value">
            <option value = "R">Random plant type</option>
            <option value = "N">Permanent disturbance</option>
            <option value = "0">Gap (temporary)</option>
            <option value = "1">Plant type 1</option>
            <option value = "2">Plant type 2</option>
            <option value = "3">Plant type 3</option>
            <option value = "4">Plant type 4</option>
            <option value = "5">Plant type 5</option>
        </select>
        """

    form_assign_cells_button = """
        <input type="submit" name="submit_value" value="Apply cell value to cells">
        """

    form_assign_area_button = """
        <input type="submit" name="submit_value" value="Apply cell value to area">
        """

    form_submit_button = """
        <br><br><input type="submit" name="submit_value" value="Submit parameters">
        """

    form_active_hidden_fields = """
        <input type="hidden" name="selected" value="%s">
        <input type="hidden" name="starting_matrix" value="%s">
        <input type="hidden" name="disturbance_only" value="%s">
        <input type="hidden" name="ongoing_disturbance" value="%s">
        <input type="hidden" name="terrain" value="%s">
        """

    form_passive_hidden_fields = """
        <input type="hidden" name="natural" value="%s">
        <input type="hidden" name="salinity" value="%s">
        <input type="hidden" name="drainage" value="%s">
        <input type="hidden" name="fertility" value="%s">
        """

    form_plant_code_hidden_field = """
        <input type="hidden" name="plant_code_%s" value="%s">
        """

    form_lifespan_hidden_field = """
        <input type="hidden" name="lifespan_%s" value="%s">
        """

    form_altitude_optimum_hidden_field = """
        <input type="hidden" name="altitude_optimum_%s" value="%s">
    """

    form_altitude_effect_hidden_field = """
        <input type="hidden" name="altitude_effect_%s" value="%s">
        """

    form_salinity_optimum_hidden_field = """
        <input type="hidden" name="salinity_optimum_%s" value="%s">
        """

    form_salinity_effect_hidden_field = """
        <input type="hidden" name="salinity_effect_%s" value="%s">
        """

    form_drainage_optimum_hidden_field = """
        <input type="hidden" name="drainage_optimum_%s" value="%s">
        """

    form_drainage_effect_hidden_field= """
        <input type="hidden" name="drainage_effect_%s" value="%s">
        """

    form_fertility_optimum_hidden_field = """
        <input type="hidden" name="fertility_optimum_%s" value="%s">
        """

    form_fertility_effect_hidden_field= """
        <input type="hidden" name="fertility_effect_%s" value="%s">
        """

    form_replace_hidden_field = """
        <input type="hidden" name="replace_%s_%s" value="%s">
    """

    form_footer = '</form>'


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

    #Region tag of the opensim region that created this record (from vMeadow.ini)
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
                Region tag: <input type="text" name="region_tag" maxlength="20" size="21"><br><br>
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
            self.response.out.write(self.record_output_label % (sim_id, region_tag))
            for record in records:
                self.response.out.write(self.record_output % (record.data, str(record.time_stamp)))
        else:
            self.response.out.write(self.no_records_output % (sim_id, region_tag))
        self.response.out.write(page.footer)

    record_output_label = """
        <p>
            <b>Records for %s in %s:</b>
        </p>
        <b>Simulation step, Gap count, Species1 count, Species2 count, Species3 count, Species4 count, Species5 count</b><br>
        """

    record_output = '%s,%s<br>'

    no_records_output = 'No records for %s in %s.'


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
application = webapp.WSGIApplication([
    ('/', LogOrParametersPage),
    ('/parametersform1', ParametersFormPageOne),
    ('/parametersform2', ParametersFormPageTwo),
    ('/parametersform3', ParametersFormPageThree),
    ('/data', GetParameters),
    ('/selectmap', SelectTerrainMap),
    ('/plants', PlantPicturesPage),
    ('/addlog', AddLogRecord),
    ('/log', LogFormPage),
    ('/getlog', GetLogRecords),
    ('/deletelog', DeleteLogRecords)], debug=True)

def main():
    run_wsgi_app(application)

if __name__ == "__main__":
    main()

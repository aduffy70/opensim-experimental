<?php
$timestamp = time();
$rows = 36;
$cols = 36;
?>
<div style="width: 1000">
<form method="POST" action="cgi-bin/conway_command_generator.php">
    <p><span style="font-size: larger;">Conway's Game of Life Pattern Generator</span></p>
    <p>This form generates starting patterns for a demonstration of Conway's Game of Life on the surface of a torus in a 3D virtual world. <br>Changes made here will not take effect until they are enabled from within the virtual world.</p>
    <p><input value="Submit" type="submit"><p>
    <p><b>Pattern Identifier:</b> A unique code to identify this starting pattern.<br> &nbsp;<input type="text" name="patternID" value="<?=$timestamp ?>" maxlength="11" size="11" readonly="readonly">&nbsp;&nbsp;<i>Generated automatically</i><br></p>
    <p><b>Starting Pattern:</b> Select cells of the matrix to set their starting status to 'ON'.<br>
    <?php for($row=0; $row<$rows; $row++) { for($col=0; $col<$cols;$col++) {?>
        <input name="cell<?php echo "r",$row,"c",$col;?>" type="checkbox">
    <?php }?></br><?php }?></p>
</form>
</div>

